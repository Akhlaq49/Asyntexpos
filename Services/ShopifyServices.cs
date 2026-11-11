using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// Shopify Admin REST API (version below). Asyntex is the master for products, prices and stock;
/// Shopify orders come back through webhooks and become Asyntex sales.
/// </summary>
public class ShopifyClient
{
    public const string ApiVersion = "2024-10";
    private readonly HttpClient _http;

    public ShopifyClient(HttpClient http) => _http = http;

    private HttpRequestMessage Req(HttpMethod method, ChannelConnection c, string path, JsonNode? body = null)
    {
        var domain = c.ShopDomain.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var req = new HttpRequestMessage(method, $"https://{domain}/admin/api/{ApiVersion}/{path}");
        req.Headers.Add("X-Shopify-Access-Token", c.AccessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        return req;
    }

    public async Task<JsonNode> SendAsync(HttpMethod method, ChannelConnection c, string path, JsonNode? body = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var res = await _http.SendAsync(Req(method, c, path, body));
            var text = await res.Content.ReadAsStringAsync();
            // Shopify allows ~2 requests/second per store; back off when throttled.
            if ((int)res.StatusCode == 429 && attempt < 3)
            {
                var wait = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                await Task.Delay(wait);
                continue;
            }
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"Shopify {(int)res.StatusCode}: {(text.Length > 400 ? text[..400] : text)}");
            return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text)!;
        }
    }

    public static bool VerifyWebhook(string secret, byte[] body, string? hmacHeader)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(hmacHeader)) return false;
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToBase64String(h.ComputeHash(body));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(hmacHeader));
    }
}

public interface IChannelSyncService
{
    Task<object> TestAsync(ChannelConnection c);
    Task PushProductAsync(ChannelConnection c, int productId);
    Task PushStockAsync(ChannelConnection c, int productId);
    Task<object> ImportOrderAsync(ChannelConnection c, JsonNode order);
    Task CancelOrderAsync(ChannelConnection c, JsonNode order);
    /// <summary>Sends pending outbox rows for the current tenant. Returns how many were sent.</summary>
    Task<int> ProcessOutboxAsync(int max = 50);
}

public class ChannelSyncService : IChannelSyncService
{
    private readonly AppDbContext _db;
    private readonly ShopifyClient _shopify;
    private readonly IInventoryService _inventory;
    private readonly IServiceProvider _services;
    private readonly ILogger<ChannelSyncService> _log;

    public ChannelSyncService(AppDbContext db, ShopifyClient shopify, IInventoryService inventory, IServiceProvider services, ILogger<ChannelSyncService> log)
    {
        _db = db;
        _shopify = shopify;
        _inventory = inventory;
        _services = services;
        _log = log;
    }

    public async Task<object> TestAsync(ChannelConnection c)
    {
        var shop = await _shopify.SendAsync(HttpMethod.Get, c, "shop.json");
        var locations = await _shopify.SendAsync(HttpMethod.Get, c, "locations.json");
        var list = locations["locations"]?.AsArray().Select(l => new { id = l?["id"]?.ToString(), name = l?["name"]?.ToString() }).ToList();
        if (string.IsNullOrWhiteSpace(c.ExternalLocationId) && list?.Count > 0) c.ExternalLocationId = list[0].id;
        return new { shop = shop["shop"]?["name"]?.ToString(), domain = shop["shop"]?["myshopify_domain"]?.ToString(), locations = list };
    }

    private async Task<string> ExternalLocationAsync(ChannelConnection c)
    {
        if (!string.IsNullOrWhiteSpace(c.ExternalLocationId)) return c.ExternalLocationId!;
        var locations = await _shopify.SendAsync(HttpMethod.Get, c, "locations.json");
        c.ExternalLocationId = locations["locations"]?.AsArray().FirstOrDefault()?["id"]?.ToString()
                               ?? throw new InvalidOperationException("The Shopify store has no locations.");
        return c.ExternalLocationId;
    }

    public async Task PushProductAsync(ChannelConnection c, int productId)
    {
        var p = await _db.Products.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == productId);
        if (p == null) return;
        var links = await _db.ChannelProductLinks.Where(l => l.ConnectionId == c.Id && l.ProductId == productId).ToListAsync();
        var variants = p.HasVariants ? await _db.ProductVariants.Where(v => v.ProductId == p.Id && v.IsActive).OrderBy(v => v.Id).ToListAsync() : new List<ProductVariant>();

        if (!p.SellOnline || p.IsPrescriptionOnly || p.IsControlled)
        {
            // Not for online sale: hide it in Shopify if it was published before.
            var existingId = links.FirstOrDefault()?.ExternalProductId;
            if (existingId != null)
                await _shopify.SendAsync(HttpMethod.Put, c, $"products/{existingId}.json", new JsonObject { ["product"] = new JsonObject { ["id"] = long.Parse(existingId), ["status"] = "draft" } });
            return;
        }

        JsonObject VariantJson(ProductVariant? v)
        {
            var o = new JsonObject
            {
                ["price"] = (v?.Price ?? p.Price).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ["sku"] = v?.Sku ?? p.SKU,
                ["barcode"] = v?.Barcode ?? p.ItemBarcode,
                ["inventory_management"] = "shopify"
            };
            if (v != null)
            {
                if (v.Option1 != null) o["option1"] = v.Option1;
                if (v.Option2 != null) o["option2"] = v.Option2;
                if (v.Option3 != null) o["option3"] = v.Option3;
                var link = links.FirstOrDefault(l => l.VariantId == v.Id);
                if (link?.ExternalVariantId != null) o["id"] = long.Parse(link.ExternalVariantId);
            }
            else if (links.FirstOrDefault(l => l.VariantId == null)?.ExternalVariantId is string vid) o["id"] = long.Parse(vid);
            return o;
        }

        var body = new JsonObject
        {
            ["title"] = p.ProductName,
            ["body_html"] = p.Description ?? "",
            ["vendor"] = p.Brand ?? p.Manufacturer ?? "",
            ["product_type"] = p.Category ?? "",
            ["status"] = "active",
            ["variants"] = new JsonArray(variants.Count > 0 ? variants.Select(v => (JsonNode)VariantJson(v)).ToArray() : new JsonNode[] { VariantJson(null) })
        };
        if (variants.Count > 0)
        {
            var optionNames = await (from o in _db.ProductOptions
                                     join a in _db.VariantAttributes on o.VariantAttributeId equals a.Id
                                     where o.ProductId == p.Id
                                     orderby o.Position
                                     select a.Name).ToListAsync();
            body["options"] = new JsonArray(optionNames.Select(n => (JsonNode)new JsonObject { ["name"] = n }).ToArray());
        }

        var productExternalId = links.FirstOrDefault()?.ExternalProductId;
        JsonNode result;
        if (productExternalId == null)
            result = await _shopify.SendAsync(HttpMethod.Post, c, "products.json", new JsonObject { ["product"] = body });
        else
        {
            body["id"] = long.Parse(productExternalId);
            result = await _shopify.SendAsync(HttpMethod.Put, c, $"products/{productExternalId}.json", new JsonObject { ["product"] = body });
        }

        var prod = result["product"]!;
        var newId = prod["id"]!.ToString();
        var remote = prod["variants"]!.AsArray();
        var targets = variants.Count > 0 ? variants.Cast<ProductVariant?>().ToList() : new List<ProductVariant?> { null };
        foreach (var v in targets)
        {
            var sku = v?.Sku ?? p.SKU;
            var match = remote.FirstOrDefault(r => sku != null && r?["sku"]?.ToString() == sku) ?? (targets.Count == 1 ? remote.FirstOrDefault() : null);
            if (match == null) continue;
            var link = links.FirstOrDefault(l => l.VariantId == v?.Id);
            if (link == null)
            {
                link = new ChannelProductLink { ConnectionId = c.Id, ProductId = p.Id, VariantId = v?.Id };
                _db.ChannelProductLinks.Add(link);
                links.Add(link);
            }
            link.ExternalProductId = newId;
            link.ExternalVariantId = match["id"]?.ToString();
            link.ExternalInventoryItemId = match["inventory_item_id"]?.ToString();
            link.LastPushedPrice = v?.Price ?? p.Price;
            link.LastSyncedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        await PushStockAsync(c, p.Id);
    }

    public async Task PushStockAsync(ChannelConnection c, int productId)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return;
        var links = await _db.ChannelProductLinks.Where(l => l.ConnectionId == c.Id && l.ProductId == productId && l.ExternalInventoryItemId != null).ToListAsync();
        if (links.Count == 0) return; // not published yet
        var location = await ExternalLocationAsync(c);
        foreach (var link in links)
        {
            var qty = product.SellOnline && !product.IsPrescriptionOnly && !product.IsControlled
                ? await _inventory.AvailableAsync(product, c.LocationId, link.VariantId, sellableOnly: true)
                : 0;
            await _shopify.SendAsync(HttpMethod.Post, c, "inventory_levels/set.json", new JsonObject
            {
                ["location_id"] = long.Parse(location),
                ["inventory_item_id"] = long.Parse(link.ExternalInventoryItemId!),
                ["available"] = Math.Max(0, qty)
            });
            link.LastPushedQty = qty;
            link.LastSyncedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<object> ImportOrderAsync(ChannelConnection c, JsonNode order)
    {
        var externalId = order["id"]!.ToString();
        var existing = await _db.ChannelOrders.FirstOrDefaultAsync(o => o.ConnectionId == c.Id && o.ExternalOrderId == externalId);
        if (existing != null) return new { duplicate = true, existing.SaleId }; // Shopify may resend the same webhook

        var log = new ChannelOrder
        {
            ConnectionId = c.Id, ExternalOrderId = externalId, ExternalNumber = order["name"]?.ToString(),
            PayloadJson = order.ToJsonString(), Status = "Imported"
        };
        _db.ChannelOrders.Add(log);
        await _db.SaveChangesAsync();

        try
        {
            var customer = await FindOrCreateCustomerAsync(order);
            var items = new List<CreateSaleItemDto>();
            foreach (var li in order["line_items"]?.AsArray() ?? new JsonArray())
            {
                var variantId = li?["variant_id"]?.ToString();
                var sku = li?["sku"]?.ToString();
                var qty = li?["quantity"]?.GetValue<int>() ?? 0;
                var price = decimal.TryParse(li?["price"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pr) ? pr : 0;
                var link = variantId == null ? null : await _db.ChannelProductLinks.FirstOrDefaultAsync(l => l.ConnectionId == c.Id && l.ExternalVariantId == variantId);
                int? productId = link?.ProductId;
                int? localVariant = link?.VariantId;
                if (productId == null && !string.IsNullOrWhiteSpace(sku))
                {
                    var v = await _db.ProductVariants.FirstOrDefaultAsync(x => x.Sku == sku);
                    if (v != null) { productId = v.ProductId; localVariant = v.Id; }
                    else productId = await _db.Products.Where(p => p.SKU == sku).Select(p => (int?)p.Id).FirstOrDefaultAsync();
                }
                if (productId == null) throw new InvalidOperationException($"Line \"{li?["title"]}\" (SKU {sku}) is not linked to an Asyntex product.");
                items.Add(new CreateSaleItemDto
                {
                    ProductId = productId.Value, ProductName = li?["title"]?.ToString() ?? "", Quantity = qty, UnitQuantity = qty, SaleUnit = "Unit",
                    PurchasePrice = price, UnitCost = price, TotalCost = price * qty, VariantId = localVariant
                });
            }
            var total = decimal.Parse(order["total_price"]?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            var tax = decimal.TryParse(order["total_tax"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tx) ? tx : 0;
            var discount = decimal.TryParse(order["total_discounts"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ds) ? ds : 0;

            var sales = _services.GetRequiredService<ISaleService>();
            var created = await sales.CreateAsync(new CreateSaleDto
            {
                CustomerId = customer?.Id, CustomerName = customer?.FullName ?? "Shopify customer", Biller = "Shopify",
                Source = "online", Channel = "Shopify", OrderNumber = log.ExternalNumber, GrandTotal = total, OrderTax = tax, Discount = discount,
                Status = "Pending", LocationId = c.LocationId, Notes = $"Shopify order {log.ExternalNumber}", Items = items
            });
            var saleId = (int)created.GetType().GetProperty("Id")!.GetValue(created)!;
            log.SaleId = saleId;
            if (order["financial_status"]?.ToString() == "paid" && total > 0)
                await sales.CreatePaymentAsync(saleId, new CreateSalePaymentDto { Reference = log.ExternalNumber ?? externalId, ReceivedAmount = total, PayingAmount = total, PaymentType = "Online", Description = "Paid on Shopify" });
            await _db.SaveChangesAsync();
            return new { saleId, log.ExternalNumber };
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            await _db.SaveChangesAsync();
            _log.LogWarning(ex, "Shopify order {Order} could not be imported", externalId);
            return new { failed = true, error = log.Error };
        }
    }

    private async Task<Party?> FindOrCreateCustomerAsync(JsonNode order)
    {
        var c = order["customer"];
        var email = (c?["email"] ?? order["email"])?.ToString()?.Trim().ToLowerInvariant();
        var phone = (c?["phone"] ?? order["phone"])?.ToString();
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone)) return null;
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Role == "Customer" && ((email != null && p.Email == email) || (phone != null && p.Phone == phone)));
        if (party != null) return party;
        var name = $"{c?["first_name"]} {c?["last_name"]}".Trim();
        party = new Party { FullName = string.IsNullOrWhiteSpace(name) ? email ?? phone ?? "Shopify customer" : name, Email = email, Phone = phone, Role = "Customer", Status = "active" };
        _db.Parties.Add(party);
        await _db.SaveChangesAsync();
        return party;
    }

    public async Task CancelOrderAsync(ChannelConnection c, JsonNode order)
    {
        var externalId = order["id"]!.ToString();
        var log = await _db.ChannelOrders.FirstOrDefaultAsync(o => o.ConnectionId == c.Id && o.ExternalOrderId == externalId);
        if (log == null || log.Status == "Cancelled") return;
        if (log.SaleId.HasValue)
        {
            var sales = _services.GetRequiredService<ISaleService>();
            foreach (var p in await _db.SalePayments.Where(x => x.SaleId == log.SaleId).ToListAsync())
                await sales.DeletePaymentAsync(log.SaleId.Value, p.Id);
            await sales.DeleteAsync(log.SaleId.Value); // puts the stock back
        }
        log.Status = "Cancelled";
        await _db.SaveChangesAsync();
    }

    public async Task<int> ProcessOutboxAsync(int max = 50)
    {
        var pending = await _db.SyncOutbox.Where(o => o.Status == "Pending").OrderBy(o => o.Id).Take(max).ToListAsync();
        if (pending.Count == 0) return 0;
        var connections = await _db.ChannelConnections.Where(c => c.Status == "Active").ToDictionaryAsync(c => c.Id);
        var sent = 0;
        foreach (var row in pending)
        {
            if (!connections.TryGetValue(row.ConnectionId, out var c)) { row.Status = "Failed"; row.LastError = "Connection paused or removed."; continue; }
            try
            {
                if (row.Kind == "Product") await PushProductAsync(c, row.EntityId);
                else await PushStockAsync(c, row.EntityId);
                row.Status = "Sent";
                row.SentAt = DateTime.UtcNow;
                c.LastSyncAt = DateTime.UtcNow;
                c.LastError = null;
                sent++;
            }
            catch (Exception ex)
            {
                row.Attempts++;
                row.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                if (row.Attempts >= 5) row.Status = "Failed";
                c.LastError = row.LastError;
            }
            await _db.SaveChangesAsync();
        }
        return sent;
    }
}

/// <summary>Sends queued channel updates every 30 seconds and a full stock refresh once a day.</summary>
public class ChannelSyncWorker : BackgroundService
{
    private readonly IServiceProvider _root;
    private readonly ILogger<ChannelSyncWorker> _log;
    private DateTime _lastFull = DateTime.MinValue;

    public ChannelSyncWorker(IServiceProvider root, ILogger<ChannelSyncWorker> log)
    {
        _root = root;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<int> tenants;
                using (var scope = _root.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    tenants = await db.ChannelConnections.IgnoreQueryFilters().Where(c => c.Status == "Active").Select(c => c.TenantId).Distinct().ToListAsync(stoppingToken);
                }
                var full = DateTime.UtcNow - _lastFull > TimeSpan.FromHours(24);
                foreach (var tenantId in tenants)
                {
                    await TenantProvider.RunAsTenantAsync(_root, tenantId, async sp =>
                    {
                        if (full)
                        {
                            var db = sp.GetRequiredService<AppDbContext>();
                            var conns = await db.ChannelConnections.Where(c => c.Status == "Active" && c.SyncStock).ToListAsync();
                            foreach (var c in conns)
                            {
                                var linked = await db.ChannelProductLinks.Where(l => l.ConnectionId == c.Id).Select(l => l.ProductId).Distinct().ToListAsync();
                                var queued = await db.SyncOutbox.Where(o => o.ConnectionId == c.Id && o.Status == "Pending" && o.Kind == "StockLevel").Select(o => o.EntityId).ToListAsync();
                                foreach (var pid in linked.Except(queued))
                                    db.SyncOutbox.Add(new SyncOutbox { TenantId = tenantId, ConnectionId = c.Id, Kind = "StockLevel", EntityId = pid });
                            }
                            await db.SaveChangesAsync();
                        }
                        return await sp.GetRequiredService<IChannelSyncService>().ProcessOutboxAsync();
                    });
                }
                if (full) _lastFull = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Channel sync pass failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
