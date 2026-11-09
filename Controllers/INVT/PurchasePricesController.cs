using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Data;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>Inventory > Purchase Prices: what the company pays for each product (INVT_PPRC)</summary>
[Authorize]
[ApiController]
[Route("api/purchase-prices")]
public class PurchasePricesController : ControllerBase
{
    private readonly AppDbContext _db;
    public PurchasePricesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await PurchasePriceService.ListAsync(_db, ct));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] List<PurchasePriceService.Change> changes, CancellationToken ct)
    {
        if (changes == null || changes.Count == 0) return BadRequest(new { message = "Nothing to save." });
        try { await PurchasePriceService.SaveAsync(_db, changes, User.FindFirstValue(ClaimTypes.Name), ct); }
        catch (InvalidOperationException e) { return BadRequest(new { message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        return Ok(await PurchasePriceService.ListAsync(_db, ct));
    }
}
