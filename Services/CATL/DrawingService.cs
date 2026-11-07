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
    // keep the same in Drawing2Quote (validator.py MAX_MB), web.config and the frontend's drawingService.ts
    public const int MaxMb = 200;
    public const long MaxBytes = MaxMb * 1024L * 1024;
    // scanned PDFs and pictures of a drawing are read from the image by Drawing2Quote (OCR + tracing)
    public static readonly string[] Extensions = { ".dxf", ".pdf", ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif", ".webp" };

    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly CatalogService _catalog;
    private readonly string? _key;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DrawingService(HttpClient http, AppDbContext db, CatalogService catalog, IConfiguration config)
    {
        _http = http;
        _db = db;
        _catalog = catalog;
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
        if (ext is ".heic" or ".heif") throw new DrawingServiceException(422, "HEIC photos can't be read. On the phone choose \"Most Compatible\" (JPG) or send it as JPG.");
        if (!Extensions.Contains(ext)) throw new DrawingServiceException(422, "Upload a DXF, a PDF, or a picture of the drawing (JPG, PNG or TIFF).");
        if (file.Length == 0) throw new DrawingServiceException(422, "The file is empty.");
        if (file.Length > MaxBytes) throw new DrawingServiceException(422, $"The file is {file.Length / 1024d / 1024:0.0} MB - the maximum is {MaxMb} MB.");

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
            extraction, catalog = await CatalogAsync(plan, ct), plan, choices, sizeId,
            otherBrand = otherBrand == null ? null : new { company = otherBrand.Company!.Trim(), areas = otherBrand.Areas },
        };
        var content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        var result = await SendAsync(Request(HttpMethod.Post, "suggest", content), ct);
        return await WithPricesAsync(result, ct);
    }

    /// <summary>Prices of the suggested lines, never stored: each line is a catalogue product (itemId); its price is
    /// its default SKU's price in the active price list, else - not on the price list - its packages-file price.
    /// line total = price x quantity; the solution's amount = the sum. notInInventory = no SKU to sell.</summary>
    private async Task<JsonElement> WithPricesAsync(JsonElement result, CancellationToken ct)
    {
        if (JsonNode.Parse(result.GetRawText()) is not JsonObject root) return result;
        var lineLists = new[] { "lines", "toConfirm", "leftOut" }.Select(k => root[k] as JsonArray).Where(a => a != null).ToList();
        static int IdOf(JsonObject l, string key) => l[key] is JsonValue v && v.TryGetValue<int>(out var id) ? id : 0;
        var prices = await _catalog.PricesAsync(lineLists.SelectMany(a => a!).OfType<JsonObject>().Select(l => IdOf(l, "itemId")).Where(id => id > 0), ct);

        foreach (var line in lineLists.SelectMany(a => a!).OfType<JsonObject>())
        {
            var qty = line["quantity"] is JsonValue q && q.TryGetValue<decimal>(out var n) ? n : 0;
            var found = prices.TryGetValue(IdOf(line, "itemId"), out var p);
            decimal? price = found ? p.Price : null;
            line["unitPrice"] = price;
            line["lineTotal"] = price == null ? null : Math.Round(price.Value * qty, 2);
            line["notInInventory"] = !found || p.SkuId == null;
        }

        if (root["totals"] is JsonObject totals && root["lines"] is JsonArray solution)
        {
            var lines = solution.OfType<JsonObject>().ToList();
            totals["amount"] = lines.Sum(l => l["lineTotal"] is JsonValue t && t.TryGetValue<decimal>(out var x) ? x : 0);
            // a SKU whose price is 0 - not counted in the amount until a price is set
            totals["unpriced"] = lines.Count(l => l["unitPrice"] is JsonValue u && u.TryGetValue<decimal>(out var x) && x == 0);
        }
        return JsonSerializer.Deserialize<JsonElement>(root.ToJsonString());
    }

    /// <summary>The company's catalogue for the plan, in the reader's format (CatalogService)</summary>
    public Task<object> CatalogAsync(string plan, CancellationToken ct) => _catalog.ReaderCatalogAsync(plan, ct);

    /// <summary>The reader's drawing rules: [{name, gangs}] - which products each uses is the company's (CatalogDrawingRuleItems)</summary>
    public async Task<JsonElement> RulesAsync(CancellationToken ct) => await SendAsync(Request(HttpMethod.Get, "rules"), ct);
}
