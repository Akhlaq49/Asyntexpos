using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MiscellaneousRegisterController : ControllerBase
{
    private readonly IMiscellaneousRegisterService _service;

    public MiscellaneousRegisterController(IMiscellaneousRegisterService service)
    {
        _service = service;
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var transactions = await _service.GetByCustomerAsync(customerId);
        return Ok(transactions);
    }

    [HttpGet("customer/{customerId}/balance")]
    public async Task<IActionResult> GetBalance(int customerId)
    {
        var balance = await _service.GetBalanceAsync(customerId);
        return Ok(balance);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMiscTransactionDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByCustomer), new { customerId = dto.CustomerId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _service.GetSummaryAsync();
        return Ok(summary);
    }
}
