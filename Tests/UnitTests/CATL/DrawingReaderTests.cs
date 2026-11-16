using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ReactPosApi.Services;
using Xunit;

namespace ReactPosApi.UnitTests.CATL;

/// <summary>Drawing to Solution: where the drawing requests go (DrawingService:Reader and its Url for the environment),
/// and the whole round trip POS -> reader -> POS against each reader that is running on this PC.</summary>
public class DrawingReaderTests
{
    /// <summary>the POS project folder (its appsettings files)</summary>
    private static string PosDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "ReactPosApi.csproj"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("ReactPosApi.csproj not found");
    }

    /// <summary>the settings the POS runs with in this environment (+ overrides)</summary>
    private static IConfiguration Settings(string environment, Dictionary<string, string?>? overrides = null)
    {
        var dir = PosDir();
        var b = new ConfigurationBuilder().AddJsonFile(Path.Combine(dir, "appsettings.json"), optional: false);
        var envFile = Path.Combine(dir, $"appsettings.{environment}.json");
        if (File.Exists(envFile)) b.AddJsonFile(envFile);
        if (overrides != null) b.AddInMemoryCollection(overrides);
        return b.Build();
    }

    [Fact]
    public void DevelopmentGoesToTheLocalReaders()
    {
        var dotnet = DrawingService.ResolveReader(Settings("Development", new() { ["DrawingService:Reader"] = "dotnet" }));
        Assert.Equal(("dotnet", "http://localhost:8766"), (dotnet.Reader, dotnet.Url));
        var python = DrawingService.ResolveReader(Settings("Development", new() { ["DrawingService:Reader"] = "python" }));
        Assert.Equal(("python", "http://127.0.0.1:8765"), (python.Reader, python.Url));
    }

    [Fact]
    public void ProductionGoesToTheDeployedReaders()
    {
        if (!File.Exists(Path.Combine(PosDir(), "appsettings.Production.json"))) return;    // (not every copy has it)
        var dotnet = DrawingService.ResolveReader(Settings("Production", new() { ["DrawingService:Reader"] = "dotnet" }));
        Assert.Equal(("dotnet", "https://drawingreader.asyntexconsultancy.com"), (dotnet.Reader, dotnet.Url));
        var python = DrawingService.ResolveReader(Settings("Production", new() { ["DrawingService:Reader"] = "PYTHON " }));
        Assert.Equal(("python", "https://abc.asyntexconsultancy.com"), (python.Reader, python.Url));
    }

    [Fact]
    public void AFixedUrlOverridesTheChoice()
    {
        var r = DrawingService.ResolveReader(Settings("Development", new() { ["DrawingService:Url"] = "http://10.0.0.5:9000/", ["DrawingService:Key"] = "k" }));
        Assert.Equal(("http://10.0.0.5:9000", "k"), (r.Url, r.Key));
    }

    [Fact]
    public void AnUnknownReaderStopsTheStartWithTheReason()
    {
        var e = Assert.Throws<InvalidOperationException>(() =>
            DrawingService.ResolveReader(Settings("Development", new() { ["DrawingService:Reader"] = "java" })));
        Assert.Contains("DrawingService:Readers:java:Url", e.Message);
    }

    // ── the round trip, against each reader running on this PC (skipped when it isn't) ──
    [Theory]
    [InlineData("dotnet")]
    [InlineData("python")]
    public async Task UploadReadAndSuggest(string reader)
    {
        using var db = new TestDb();
        var config = Settings("Development", new() { ["DrawingService:Reader"] = reader });
        var service = new DrawingService(new HttpClient(), db.Db, new CatalogService(db.Db), config);
        Assert.Equal(reader, service.Reader);
        if (!await service.IsRunningAsync(CancellationToken.None)) return;      // that reader isn't started here

        // the upload goes to the reader's job queue; the POS polls until the reading is done
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "CATL", "data", "drawing-sample.dxf"));
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "plan.dxf") { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };
        var job = await service.ExtractAsync(file, CancellationToken.None);
        var id = job.GetProperty("jobId").GetString()!;
        JsonElement status = default;
        for (int i = 0; i < 120; i++)
        {
            status = await service.JobAsync(id, CancellationToken.None);
            if (status.GetProperty("status").GetString() is "done" or "failed") break;
            await Task.Delay(500);
        }
        Assert.Equal("done", status.GetProperty("status").GetString());
        var extraction = status.GetProperty("result");
        Assert.Equal("dxf", extraction.GetProperty("fileType").GetString());
        Assert.Equal(51, extraction.GetProperty("totals").GetProperty("points").GetInt32());
        Assert.Equal(new[] { "Ground Floor", "First Floor" }, extraction.GetProperty("floors").EnumerateArray().Select(f => f.GetProperty("name").GetString()));

        // a refused file is answered straight away, with the reason for the user
        var dwg = Encoding.ASCII.GetBytes("AC1027");
        var bad = new FormFile(new MemoryStream(dwg), 0, dwg.Length, "file", "plan.dxf") { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };
        var refused = await Assert.ThrowsAsync<DrawingServiceException>(() => service.ExtractAsync(bad, CancellationToken.None));
        Assert.Equal(422, refused.StatusCode);
        Assert.Contains("DXF", refused.Message);

        // the reviewed drawing back to the reader for the suggestion (this company's catalogue: empty here), priced here
        var suggestion = await service.SuggestAsync(extraction, "standard", null, null, null, CancellationToken.None);
        Assert.Equal("standard", suggestion.GetProperty("plan").GetString());
        Assert.False(suggestion.GetProperty("needsConfirmation").GetBoolean());
        Assert.True(suggestion.GetProperty("totals").TryGetProperty("amount", out _));     // (joined by the POS)

        var rules = await service.RulesAsync(CancellationToken.None);
        Assert.Contains(rules.EnumerateArray(), r => r.GetProperty("name").GetString() == "Switch boards");
    }
}
