using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Data;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>Inventory > Sale Prices: each product's purchase price (from its latest received purchase), the sale
/// price people set, and the profit between them</summary>
[Authorize]
[ApiController]
[Route("api/sale-prices")]
public class SalePricesController : ControllerBase
{
    private readonly AppDbContext _db;
    public SalePricesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await PurchasePriceService.ListAsync(_db, ct));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] List<PurchasePriceService.SaleChange> changes, CancellationToken ct)
    {
        if (changes == null || changes.Count == 0) return BadRequest(new { message = "Nothing to save." });
        try { await PurchasePriceService.SaveSalePricesAsync(_db, changes, ct); }
        catch (InvalidOperationException e) { return BadRequest(new { message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        return Ok(await PurchasePriceService.ListAsync(_db, ct));
    }
}
