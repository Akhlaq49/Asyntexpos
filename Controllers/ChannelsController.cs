using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

public class ChannelConnectionDto
{
    public string Channel { get; set; } = "Shopify";
    public string ShopDomain { get; set; } = string.Empty;
    /// <summary>Send back the masked value to keep the saved token.</summary>
    public string? AccessToken { get; set; }
    public string? WebhookSecret { get; set; }
    public int LocationId { get; set; }
    public string? ExternalLocationId { get; set; }
    public bool SyncProducts { get; set; } = true;
    public bool SyncStock { get; set; } = true;
    public bool SyncPrices { get; set; } = true;
    public bool ImportOrders { get; set; } = true;
    public string Status { get; set; } = "Active";
}

/// <summary>Online sales channels (Shopify): connection, sync controls, logs and the order webhook.</summary>
[Authorize]
[ApiController]
[Route("api/channels")]
public class ChannelsController : ControllerBase
{
    private const string Mask = "••••••••";
    private readonly AppDbContext _db;
    private readonly IChannelSyncService _sync;
    private readonly IAuditService _audit;

    public ChannelsController(AppDbContext db, IChannelSyncService sync, IAuditService audit)
    {
        _db = db;
        _sync = sync;
        _audit = audit;
    }

    private object Map(ChannelConnection c) => new
    {
        c.Id, c.Channel, c.ShopDomain, accessToken = string.IsNullOrEmpty(c.AccessToken) ? "" : Mask,
        webhookSecret = string.IsNullOrEmpty(c.WebhookSecret) ? "" : Mask, c.LocationId, c.ExternalLocationId,
        c.SyncProducts, c.SyncStock, c.SyncPrices, c.ImportOrders, c.Status, c.LastError, c.LastSyncAt, c.CreatedAt,
        webhookUrl = $"{Request.Scheme}://{Request.Host}/api/channels/shopify/webhook/{c.TenantId}/{c.Id}"
    };

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _db.ChannelConnections.OrderBy(c => c.Id).ToListAsync();
        var linked = await _db.ChannelProductLinks.GroupBy(l => l.ConnectionId).Select(g => new { Id = g.Key, N = g.Select(x => x.ProductId).Distinct().Count() }).ToListAsync();
        var pending = await _db.SyncOutbox.Where(o => o.Status == "Pending").GroupBy(o => o.ConnectionId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync();
        var failed = await _db.SyncOutbox.Where(o => o.Status == "Failed").GroupBy(o => o.ConnectionId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync();
        return Ok(list.Select(c => new
        {
            connection = Map(c),
            linkedProducts = linked.FirstOrDefault(x => x.Id == c.Id)?.N ?? 0,
            pending = pending.FirstOrDefault(x => x.Id == c.Id)?.N ?? 0,
            failed = failed.FirstOrDefault(x => x.Id == c.Id)?.N ?? 0
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChannelConnectionDto dto) => Ok(Map(await SaveAsync(null, dto)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ChannelConnectionDto dto) => Ok(Map(await SaveAsync(id, dto)));

    private async Task<ChannelConnection> SaveAsync(int? id, ChannelConnectionDto dto)
    {
        if (!_audit.IsAdmin) throw new BusinessRuleException("Only a manager can change online channels.");
        if (string.IsNullOrWhiteSpace(dto.ShopDomain)) throw new BusinessRuleException("Enter the Shopify store address (yourstore.myshopify.com).");
        if (!await _db.Locations.AnyAsync(l => l.Id == dto.LocationId)) throw new BusinessRuleException("Choose which branch's stock is published.");
        var c = id.HasValue ? await _db.ChannelConnections.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Channel not found") : new ChannelConnection();
        c.Channel = "Shopify";
        c.ShopDomain = dto.ShopDomain.Trim().Replace("https://", "").TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(dto.AccessToken) && dto.AccessToken != Mask) c.AccessToken = dto.AccessToken.Trim();
        if (dto.WebhookSecret != Mask) c.WebhookSecret = string.IsNullOrWhiteSpace(dto.WebhookSecret) ? c.WebhookSecret : dto.WebhookSecret.Trim();
        if (string.IsNullOrWhiteSpace(c.AccessToken)) throw new BusinessRuleException("Enter the Admin API access token from the Shopify app.");
        c.LocationId = dto.LocationId;
        c.ExternalLocationId = string.IsNullOrWhiteSpace(dto.ExternalLocationId) ? c.ExternalLocationId : dto.ExternalLocationId.Trim();
        c.SyncProducts = dto.SyncProducts; c.SyncStock = dto.SyncStock; c.SyncPrices = dto.SyncPrices; c.ImportOrders = dto.ImportOrders;
        c.Status = dto.Status is "Paused" ? "Paused" : "Active";
        if (!id.HasValue) _db.ChannelConnections.Add(c);
        _audit.Log(id.HasValue ? "Updated" : "Created", "ChannelConnection", c.ShopDomain, new { c.LocationId, c.Status });
        await _db.SaveChangesAsync();
        return c;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_audit.IsAdmin) return Forbid();
        var c = await _db.ChannelConnections.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        _db.SyncOutbox.RemoveRange(_db.SyncOutbox.Where(o => o.ConnectionId == id));
        _db.ChannelProductLinks.RemoveRange(_db.ChannelProductLinks.Where(o => o.ConnectionId == id));
        _db.ChannelConnections.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> Test(int id)
    {
        var c = await _db.ChannelConnections.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        try
        {
            var result = await _sync.TestAsync(c);
            c.LastError = null;
            await _db.SaveChangesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            c.LastError = ex.Message;
            await _db.SaveChangesAsync();
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Queues every online product (or the given ones) to be published.</summary>
    [HttpPost("{id:int}/sync-products")]
    public async Task<IActionResult> SyncProducts(int id, [FromBody] List<int>? productIds)
    {
        var c = await _db.ChannelConnections.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        var ids = productIds?.Count > 0 ? productIds
            : await _db.Products.Where(p => p.SellOnline && !p.IsPrescriptionOnly && !p.IsControlled && !p.IsMenuItem).Select(p => p.Id).ToListAsync();
        var queued = await _db.SyncOutbox.Where(o => o.ConnectionId == id && o.Status == "Pending" && o.Kind == "Product").Select(o => o.EntityId).ToListAsync();
        foreach (var pid in ids.Except(queued))
            _db.SyncOutbox.Add(new SyncOutbox { ConnectionId = id, Kind = "Product", EntityId = pid });
        await _db.SaveChangesAsync();
        return Ok(new { queued = ids.Except(queued).Count() });
    }

    [HttpPost("{id:int}/sync-stock")]
    public async Task<IActionResult> SyncStock(int id)
    {
        var ids = await _db.ChannelProductLinks.Where(l => l.ConnectionId == id).Select(l => l.ProductId).Distinct().ToListAsync();
        var queued = await _db.SyncOutbox.Where(o => o.ConnectionId == id && o.Status == "Pending" && o.Kind == "StockLevel").Select(o => o.EntityId).ToListAsync();
        foreach (var pid in ids.Except(queued))
            _db.SyncOutbox.Add(new SyncOutbox { ConnectionId = id, Kind = "StockLevel", EntityId = pid });
        await _db.SaveChangesAsync();
        return Ok(new { queued = ids.Except(queued).Count() });
    }

    /// <summary>Sends this tenant's queue now instead of waiting for the background worker.</summary>
    [HttpPost("process-now")]
    public async Task<IActionResult> ProcessNow() => Ok(new { sent = await _sync.ProcessOutboxAsync(100) });

    [HttpPost("{id:int}/retry-failed")]
    public async Task<IActionResult> RetryFailed(int id)
    {
        var rows = await _db.SyncOutbox.Where(o => o.ConnectionId == id && o.Status == "Failed").ToListAsync();
        foreach (var r in rows) { r.Status = "Pending"; r.Attempts = 0; }
        var orders = await _db.ChannelOrders.Where(o => o.ConnectionId == id && o.Status == "Failed").ToListAsync();
        var c = await _db.ChannelConnections.FirstAsync(x => x.Id == id);
        await _db.SaveChangesAsync();
        var reimported = 0;
        foreach (var o in orders)
        {
            var payload = JsonNode.Parse(o.PayloadJson ?? "{}")!;
            _db.ChannelOrders.Remove(o);
            await _db.SaveChangesAsync();
            await _sync.ImportOrderAsync(c, payload);
            reimported++;
        }
        return Ok(new { requeued = rows.Count, reimported });
    }

    [HttpGet("{id:int}/log")]
    public async Task<IActionResult> Log(int id)
    {
        var outbox = await _db.SyncOutbox.Where(o => o.ConnectionId == id).OrderByDescending(o => o.Id).Take(200).ToListAsync();
        var orders = await _db.ChannelOrders.Where(o => o.ConnectionId == id).OrderByDescending(o => o.Id).Take(200)
            .Select(o => new { o.Id, o.ExternalOrderId, o.ExternalNumber, o.SaleId, o.Status, o.Error, o.ImportedAt }).ToListAsync();
        var pids = outbox.Select(o => o.EntityId).Distinct().ToList();
        var names = await _db.Products.Where(p => pids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProductName);
        return Ok(new
        {
            outbox = outbox.Select(o => new { o.Id, o.Kind, o.EntityId, product = names.GetValueOrDefault(o.EntityId), o.Status, o.Attempts, o.LastError, o.CreatedAt, o.SentAt }),
            orders
        });
    }
}

/// <summary>Public endpoint Shopify calls. The tenant and connection are in the URL; the HMAC proves it came from Shopify.</summary>
[AllowAnonymous]
[ApiController]
[Route("api/channels/shopify/webhook")]
public class ShopifyWebhookController : ControllerBase
{
    private readonly IServiceProvider _root;
    public ShopifyWebhookController(IServiceProvider root) => _root = root;

    [HttpPost("{tenantId:int}/{connectionId:int}")]
    public async Task<IActionResult> Receive(int tenantId, int connectionId)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();
        var topic = Request.Headers["X-Shopify-Topic"].ToString();
        var hmac = Request.Headers["X-Shopify-Hmac-Sha256"].ToString();

        return await TenantProvider.RunAsTenantAsync<IActionResult>(_root, tenantId, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var c = await db.ChannelConnections.FirstOrDefaultAsync(x => x.Id == connectionId);
            if (c == null) return NotFound();
            if (!ShopifyClient.VerifyWebhook(c.WebhookSecret ?? "", body, hmac)) return Unauthorized();
            if (c.Status != "Active" || !c.ImportOrders) return Ok(new { ignored = true });
            var payload = JsonNode.Parse(body);
            if (payload == null) return BadRequest();
            var sync = sp.GetRequiredService<IChannelSyncService>();
            switch (topic)
            {
                case "orders/create":
                case "orders/paid":
                    return Ok(await sync.ImportOrderAsync(c, payload));
                case "orders/cancelled":
                    await sync.CancelOrderAsync(c, payload);
                    return Ok(new { cancelled = true });
                default:
                    return Ok(new { ignored = topic });
            }
        });
    }
}
