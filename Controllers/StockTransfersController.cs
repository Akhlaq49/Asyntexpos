using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/stock-transfers")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _service;
    public StockTransfersController(IStockTransferService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStockTransferDto dto)
        => Ok(await _service.CreateAsync(dto));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateStockTransferDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await _service.GetAsync(id);
        return t == null ? NotFound() : Ok(t);
    }

    /// <summary>Stock leaves the sender and shows as in transit.</summary>
    [HttpPost("{id:int}/dispatch")]
    public async Task<IActionResult> Dispatch(int id) => Ok(await _service.DispatchAsync(id));

    /// <summary>The receiver confirms what arrived; short quantities are recorded.</summary>
    [HttpPost("{id:int}/receive")]
    public async Task<IActionResult> Receive(int id, [FromBody] ReceiveStockTransferDto? dto) => Ok(await _service.ReceiveAsync(id, dto));

    /// <summary>Cancels a draft, or an in-transit transfer (stock goes back to the sender).</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id) => Ok(await _service.CancelAsync(id));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}
