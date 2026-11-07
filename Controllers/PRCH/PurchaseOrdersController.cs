using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;
    public PurchaseOrdersController(IPurchaseOrderService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<PurchaseOrderDto>>> GetAll([FromQuery] string? status, [FromQuery] string? search)
        => Ok(await _service.GetAllAsync(status, search));

    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(int id)
    {
        var po = await _service.GetByIdAsync(id);
        return po == null ? NotFound() : Ok(po);
    }

    [HttpPost]
    public Task<ActionResult<PurchaseOrderDto>> Create([FromBody] SavePurchaseOrderDto dto)
        => Run(async () => (PurchaseOrderDto?)await _service.CreateAsync(dto, User.FindFirstValue(ClaimTypes.Name)));

    [HttpPut("{id}")]
    public Task<ActionResult<PurchaseOrderDto>> Update(int id, [FromBody] SavePurchaseOrderDto dto)
        => Run(() => _service.UpdateAsync(id, dto));

    /// <summary>Draft -> Ordered (sent to the supplier)</summary>
    [HttpPost("{id}/mark-ordered")]
    public Task<ActionResult<PurchaseOrderDto>> MarkOrdered(int id)
        => Run(() => _service.MarkOrderedAsync(id));

    /// <summary>Receive goods (all or part): adds stock and records a Received purchase</summary>
    [HttpPost("{id}/receive")]
    public Task<ActionResult<PurchaseOrderDto>> Receive(int id, [FromBody] ReceivePurchaseOrderDto dto)
        => Run(() => _service.ReceiveAsync(id, dto, User.FindFirstValue(ClaimTypes.Name)));

    [HttpPost("{id}/cancel")]
    public Task<ActionResult<PurchaseOrderDto>> Cancel(int id)
        => Run(() => _service.CancelAsync(id));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await _service.DeleteAsync(id) ? NoContent() : NotFound();
        }
        catch (PurchaseOrderException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<ActionResult<PurchaseOrderDto>> Run(Func<Task<PurchaseOrderDto?>> action)
    {
        try
        {
            var result = await action();
            return result == null ? NotFound() : Ok(result);
        }
        catch (PurchaseOrderException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
