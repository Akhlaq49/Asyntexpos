using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly ISaleService _service;
    public SalesController(ISaleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? source = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] string? paymentStatus = null)
    {
        if (page.HasValue)
        {
            var query = new PaginationQuery { Page = page.Value, PageSize = pageSize ?? 10, Search = search, Status = status };
            return Ok(await _service.GetAllPagedAsync(source, query, paymentStatus));
        }
        return Ok(await _service.GetAllAsync(source));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await _service.GetByIdAsync(id);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleDto dto)
        => Ok(await _service.CreateAsync(dto));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateSaleDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    // Payment sub-resources
    [HttpGet("{id}/payments")]
    public async Task<IActionResult> GetPayments(int id)
    {
        var payments = await _service.GetPaymentsAsync(id);
        if (payments == null) return NotFound();
        return Ok(payments);
    }

    /// <summary>Apply payment to one sale (unchanged). For cross-order FIFO use POST customer-fifo-payment.</summary>
    [HttpPost("{id}/payments")]
    public async Task<IActionResult> CreatePayment(int id, [FromBody] CreateSalePaymentDto dto)
    {
        var result = await _service.CreatePaymentAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>Apply one payment across all outstanding sales for a customer, oldest bill first (FIFO). POS Orders grouped view.</summary>
    [HttpPost("customer-fifo-payment")]
    public async Task<IActionResult> ApplyCustomerFifoPayment([FromBody] CustomerFifoPaymentRequestDto dto)
    {
        var (result, error) = await _service.ApplyCustomerFifoPaymentAsync(dto);
        if (error != null) return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPut("{id}/payments/{paymentId}")]
    public async Task<IActionResult> UpdatePayment(int id, int paymentId, [FromBody] CreateSalePaymentDto dto)
    {
        var result = await _service.UpdatePaymentAsync(id, paymentId, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}/payments/{paymentId}")]
    public async Task<IActionResult> DeletePayment(int id, int paymentId)
    {
        var success = await _service.DeletePaymentAsync(id, paymentId);
        if (!success) return NotFound();
        return NoContent();
    }
}
