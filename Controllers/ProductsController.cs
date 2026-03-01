using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? search)
    {
        if (page.HasValue)
        {
            var query = new PaginationQuery { Page = page.Value, PageSize = pageSize ?? 10, Search = search };
            return Ok(await _service.GetAllPagedAsync(query));
        }
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("expired")]
    public async Task<ActionResult<List<ProductDto>>> GetExpired()
        => Ok(await _service.GetExpiredAsync());

    [HttpPut("expired/{id}")]
    public async Task<ActionResult<ProductDto>> UpdateExpired(int id, [FromBody] UpdateExpiredDto dto)
    {
        var result = await _service.UpdateExpiredAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("expired/{id}")]
    public async Task<IActionResult> DeleteExpired(int id)
    {
        var (success, error) = await _service.DeleteExpiredAsync(id);
        if (error != null) return Conflict(new { message = error });
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var dto = await _service.GetByIdAsync(id);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpGet("low-stocks")]
    public async Task<ActionResult<List<ProductDto>>> GetLowStocks()
        => Ok(await _service.GetLowStocksAsync());

    [HttpGet("out-of-stocks")]
    public async Task<ActionResult<List<ProductDto>>> GetOutOfStocks()
        => Ok(await _service.GetOutOfStocksAsync());

    [HttpPut("low-stocks/{id}")]
    public async Task<ActionResult<ProductDto>> UpdateLowStock(int id, [FromBody] UpdateLowStockDto dto)
    {
        var result = await _service.UpdateLowStockAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromForm] ProductFormModel form)
    {
        var result = await _service.CreateAsync(form);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> Update(int id, [FromForm] ProductFormModel form)
    {
        var result = await _service.UpdateAsync(id, form);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _service.DeleteAsync(id);
        if (error != null) return Conflict(new { message = error });
        if (!success) return NotFound();
        return NoContent();
    }
}

// Form model for multipart binding
public class ProductFormModel
{
    public string? Store { get; set; }
    public string? Warehouse { get; set; }
    public string? ProductName { get; set; }
    public string? Slug { get; set; }
    public string? Sku { get; set; }
    public string? SellingType { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Brand { get; set; }
    public string? Unit { get; set; }
    public string? BarcodeSymbology { get; set; }
    public string? ItemBarcode { get; set; }
    public string? Description { get; set; }
    public string? ProductType { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? TaxType { get; set; }
    public string? Tax { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public int QuantityAlert { get; set; }
    public string? Warranty { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturedDate { get; set; }
    public string? ExpiryDate { get; set; }
    public List<IFormFile>? Images { get; set; }
}
