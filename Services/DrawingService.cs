using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;

namespace ReactPosApi.Services;

/// <summary>The drawing reader (Python "Drawing2Quote" service) could not do it - Message is shown to the user</summary>
public class DrawingServiceException : Exception
{
    public int StatusCode { get; }
    public DrawingServiceException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

/// <summary>
/// Hybrid drawing -> solution: the Python service reads the drawing and works out the solution; this side
/// checks the user, validates the upload and gives the service the company's solution data from the database
/// (sizes, package items and quantities, the Standard / Premium rule, the linked inventory products).
/// The service estimates products and quantities only; the prices are then joined here from the Products table
/// (each line's linked inventory product): unitPrice and lineTotal per line, the amount in totals.
/// Settings: DrawingService:Url (default http://127.0.0.1:8765) and DrawingService:Key (sent as X-Service-Key).
/// </summary>
public class DrawingService
{
    public const long MaxBytes = 50L * 1024 * 1024;
    public static readonly string[] Extensions = { ".dxf", ".pdf" };

    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly string? _key;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DrawingService(HttpClient http, AppDbContext db, IConfiguration config)
    {
        _http = http;
        _db = db;
        _http.BaseAddress = new Uri((config["DrawingService:Url"] ?? "http://127.0.0.1:8765").TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromMinutes(3);
        _key = config["DrawingService:Key"];
    }

    private HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, path) { Content = content };
        if (!string.IsNullOrEmpty(_key)) req.Headers.Add("X-Service-Key", _key);
        return req;
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        HttpResponseMessage res;
        try { res = await _http.SendAsync(req, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DrawingServiceException(503, "The drawing reader is not running. Start the Drawing2Quote service and try again.");
        }
        using (res)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            if (res.IsSuccessStatusCode) return JsonDocument.Parse(body).RootElement.Clone();
            // FastAPI errors: {"detail": "..."}
            string message = "The drawing reader could not handle this request.";
            try
            {
                var detail = JsonDocument.Parse(body).RootElement.GetProperty("detail");
                message = detail.ValueKind == JsonValueKind.String ? detail.GetString()! : detail.ToString();
            }
            catch { /* not JSON */ }
            var status = res.StatusCode switch { HttpStatusCode.UnprocessableEntity => 422, HttpStatusCode.NotFound => 404, _ => 502 };
            throw new DrawingServiceException(status, message);
        }
    }

    public async Task<bool> IsRunningAsync(CancellationToken ct)
    {
        try { using var res = await _http.SendAsync(Request(HttpMethod.Get, "health"), ct); return res.IsSuccessStatusCode; }
        catch { return false; }
    }

    /// <summary>Starts reading the drawing in the background (a printed PDF is read with OCR, about a minute per
    /// sheet) and returns {jobId}; ask JobAsync for progress and, when done, the floors, areas and point counts.
    /// A file that can't be read is refused straight away (422 with the reason).</summary>
    public async Task<JsonElement> ExtractAsync(IFormFile file, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".dwg") throw new DrawingServiceException(422, "DWG files can't be read. In AutoCAD use Save As -> DXF and upload the DXF file.");
        if (!Extensions.Contains(ext)) throw new DrawingServiceException(422, "Only DXF files and vector PDFs exported from CAD can be read.");
        if (file.Length == 0) throw new DrawingServiceException(422, "The file is empty.");
        if (file.Length > MaxBytes) throw new DrawingServiceException(422, $"The file is {file.Length / 1024d / 1024:0.0} MB - the maximum is 50 MB.");

        using var form = new MultipartFormDataContent();
        var stream = new StreamContent(file.OpenReadStream());
        stream.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(stream, "file", Path.GetFileName(file.FileName));
        return await SendAsync(Request(HttpMethod.Post, "jobs", form), ct);
    }

    /// <summary>{status: queued|running|done|failed, page, pages, message, result | detail}</summary>
    public async Task<JsonElement> JobAsync(string jobId, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(jobId, "^[0-9a-f]{32}$"))
            throw new DrawingServiceException(404, "This reading is no longer available - upload the drawing again.");
        return await SendAsync(Request(HttpMethod.Get, $"jobs/{jobId}"), ct);
    }

    /// <summary>The suggested solution for the (reviewed) drawing and plan; Premium lists the "both" products to confirm first</summary>
    public async Task<JsonElement> SuggestAsync(JsonElement extraction, string plan, Dictionary<string, string>? choices, int? sizeId,
        Controllers.DrawingsController.OtherBrandDto? otherBrand, CancellationToken ct)
    {
        var body = new
        {
            extraction, catalog = await CatalogAsync(ct), plan, choices, sizeId,
            otherBrand = otherBrand == null ? null : new { company = otherBrand.Company!.Trim(), areas = otherBrand.Areas },
        };
        var content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        var result = await SendAsync(Request(HttpMethod.Post, "suggest", content), ct);
        return await WithPricesAsync(result, ct);
    }

    /// <summary>Joins the suggested lines with the Products table on productId: unit price (the product's
    /// current price), line total = price x quantity, and the solution's amount. Unlinked lines have no price.</summary>
    private async Task<JsonElement> WithPricesAsync(JsonElement result, CancellationToken ct)
    {
        if (JsonNode.Parse(result.GetRawText()) is not JsonObject root) return result;
        var lineLists = new[] { "lines", "toConfirm", "leftOut" }.Select(k => root[k] as JsonArray).Where(a => a != null).ToList();
        var ids = lineLists.SelectMany(a => a!).OfType<JsonObject>()
            .Select(l => l["productId"] is JsonValue v && v.TryGetValue<int>(out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();
        var prices = await _db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Price, ct);

        foreach (var line in lineLists.SelectMany(a => a!).OfType<JsonObject>())
        {
            var id = line["productId"] is JsonValue v && v.TryGetValue<int>(out var pid) ? pid : 0;
            var qty = line["quantity"] is JsonValue q && q.TryGetValue<decimal>(out var n) ? n : 0;
            if (prices.TryGetValue(id, out var price))
            {
                line["unitPrice"] = price;
                line["lineTotal"] = Math.Round(price * qty, 2);
            }
            else
            {
                line["unitPrice"] = null;
                line["lineTotal"] = null;
            }
        }

        if (root["totals"] is JsonObject totals && root["lines"] is JsonArray solution)
        {
            var lines = solution.OfType<JsonObject>().ToList();
            totals["amount"] = lines.Sum(l => l["lineTotal"] is JsonValue t && t.TryGetValue<decimal>(out var x) ? x : 0);
            // linked to a product whose price is 0 - not counted in the amount until a price is set
            totals["unpriced"] = lines.Count(l => l["unitPrice"] is JsonValue u && u.TryGetValue<decimal>(out var x) && x == 0);
        }
        return JsonSerializer.Deserialize<JsonElement>(root.ToJsonString());
    }

    /// <summary>The company's solution data (Inventory > Solution Packages) in the service's format</summary>
    public async Task<object> CatalogAsync(CancellationToken ct)
    {
        var sizes = await _db.PackageSizes.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var groups = await _db.SolutionGroups.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var lines = await _db.SolutionProductLines.AsNoTracking().OrderBy(l => l.SortOrder).ToListAsync(ct);
        var lineById = lines.ToDictionary(l => l.Id);
        var items = await _db.PackageItems.AsNoTracking().OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToListAsync(ct);
        var qty = (await _db.PackageQuantities.AsNoTracking().Where(q => q.Quantity > 0).ToListAsync(ct))
            .GroupBy(q => q.PackageItemId).ToDictionary(g => g.Key, g => g.ToDictionary(q => q.PackageSizeId.ToString(), q => q.Quantity));
        var productIds = items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).ToList();
        var products = await _db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductName }).ToDictionaryAsync(p => p.Id, ct);

        return new
        {
            sizes,
            items = items.Select(i =>
            {
                var line = i.ProductLineId != null && lineById.TryGetValue(i.ProductLineId.Value, out var l) ? l : null;
                var product = i.ProductId != null && products.TryGetValue(i.ProductId.Value, out var p) ? p : null;
                return new
                {
                    i.Id, i.Name,
                    groupName = line != null && groups.TryGetValue(line.GroupId, out var gn) ? gn : null,
                    productLineName = line?.Name,
                    usage = SolutionUsage.Of(line),
                    productId = product?.Id,
                    productName = product?.ProductName,
                    quantities = qty.TryGetValue(i.Id, out var q) ? q : new Dictionary<string, int>(),
                };
            }),
            lines = lines.Select(l => new { l.Name, groupName = groups.TryGetValue(l.GroupId, out var gn) ? gn : null, usage = SolutionUsage.Of(l) }),
        };
    }
}
