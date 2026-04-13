using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Public storefront API — no authentication required.
/// Used by the online store (nest-react-frontend).
/// All business logic delegated to IStorefrontService.
/// </summary>
[ApiController]
[Route("api/storefront")]
public class StorefrontController : ControllerBase
{
    private readonly IStorefrontService _svc;
    private readonly IWebContentService _webContentSvc;

    public StorefrontController(IStorefrontService svc, IWebContentService webContentSvc)
    {
        _svc = svc;
        _webContentSvc = webContentSvc;
    }

    // ──────────────────────────────────────────
    // WEB CONTENT (cached, public)
    // ──────────────────────────────────────────

    [HttpGet("webcontent")]
    public async Task<IActionResult> GetWebContent()
    {
        var data = await _webContentSvc.GetStorefrontContentAsync();
        return Ok(new { success = true, data });
    }

    // ──────────────────────────────────────────
    // CATEGORIES
    // ──────────────────────────────────────────

    [HttpGet("categories/all")]
    public async Task<IActionResult> GetAllCategories()
    {
        var data = await _svc.GetAllCategoriesAsync();
        return Ok(new { success = true, data });
    }

    [HttpGet("categories/{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var data = await _svc.GetCategoryByIdAsync(id);
        if (data == null) return NotFound(new { success = false, message = "Category not found" });
        return Ok(new { success = true, data });
    }

    // ──────────────────────────────────────────
    // PRODUCTS
    // ──────────────────────────────────────────

    [HttpGet("products/all")]
    public async Task<IActionResult> GetAllProducts()
    {
        var data = await _svc.GetAllProductsAsync();
        return Ok(new { success = true, data });
    }

    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProductById(int id)
    {
        var data = await _svc.GetProductByIdAsync(id);
        if (data == null) return NotFound(new { success = false, message = "Product not found" });
        return Ok(new { success = true, data });
    }

    [HttpGet("products/category/{categoryId}")]
    public async Task<IActionResult> GetProductsByCategory(int categoryId)
    {
        var data = await _svc.GetProductsByCategoryAsync(categoryId);
        return Ok(new { success = true, data });
    }

    [HttpGet("products/subcategory/{subCategoryId}")]
    public async Task<IActionResult> GetProductsBySubCategory(int subCategoryId)
    {
        var data = await _svc.GetProductsBySubCategoryAsync(subCategoryId);
        return Ok(new { success = true, data });
    }

    [HttpGet("products/search")]
    public async Task<IActionResult> SearchProducts([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new { success = true, data = new List<StorefrontProductDto>() });

        var data = await _svc.SearchProductsAsync(q);
        return Ok(new { success = true, data });
    }

    // ──────────────────────────────────────────
    // ORDERS
    // ──────────────────────────────────────────

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] StorefrontOrderDto dto)
    {
        var result = await _svc.CreateOrderAsync(dto);
        return Ok(new
        {
            success = true,
            data = new { id = result.Id, orderNumber = result.OrderNumber },
            message = "Order placed successfully"
        });
    }

    [HttpGet("orders/email/{email}")]
    public async Task<IActionResult> GetOrdersByEmail(string email)
    {
        var data = await _svc.GetOrdersByEmailAsync(email);
        return Ok(new { success = true, data });
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> GetOrderById(int id)
    {
        var data = await _svc.GetOrderByIdAsync(id);
        if (data == null) return NotFound(new { success = false, message = "Order not found" });
        return Ok(new { success = true, data });
    }

    [HttpGet("orders/number/{orderNumber}")]
    public async Task<IActionResult> GetOrderByNumber(string orderNumber)
    {
        var data = await _svc.GetOrderByOrderNumberAsync(orderNumber);
        if (data == null) return NotFound(new { success = false, message = "Order not found" });
        return Ok(new { success = true, data });
    }

    // ──────────────────────────────────────────
    // AUTH
    // ──────────────────────────────────────────

    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] StorefrontRegisterDto dto)
    {
        var result = await _svc.RegisterAsync(dto);
        if (!result.Success)
        {
            return result.StatusCode == 409
                ? Conflict(new { success = false, message = result.ErrorMessage })
                : BadRequest(new { success = false, message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new { customer = result.Customer },
            message = "Registration successful"
        });
    }

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] StorefrontLoginDto dto)
    {
        var result = await _svc.LoginAsync(dto);
        if (!result.Success)
            return Unauthorized(new { success = false, message = result.ErrorMessage });

        return Ok(new
        {
            success = true,
            data = new { customer = result.Customer },
            message = "Login successful"
        });
    }

    // ──────────────────────────────────────────
    // CUSTOMER / ACCOUNT MANAGEMENT
    // ──────────────────────────────────────────

    [HttpGet("customers/email/{email}")]
    public async Task<IActionResult> GetCustomerByEmail(string email)
    {
        var data = await _svc.GetCustomerByEmailAsync(email);
        if (data == null) return NotFound(new { success = false, message = "Customer not found" });
        return Ok(new { success = true, data });
    }

    [HttpGet("customers/{id}")]
    public async Task<IActionResult> GetCustomerById(int id)
    {
        var data = await _svc.GetCustomerByIdAsync(id);
        if (data == null) return NotFound(new { success = false, message = "Customer not found" });
        return Ok(new { success = true, data });
    }

    [HttpPut("customers/{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] StorefrontUpdateCustomerDto dto)
    {
        var data = await _svc.UpdateCustomerAsync(id, dto);
        if (data == null) return NotFound(new { success = false, message = "Customer not found" });
        return Ok(new { success = true, data, message = "Account updated successfully" });
    }

    [HttpPost("customers/{id}/change-password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] StorefrontChangePasswordDto dto)
    {
        var ok = await _svc.ChangePasswordAsync(id, dto);
        if (!ok) return BadRequest(new { success = false, message = "Current password is incorrect" });
        return Ok(new { success = true, message = "Password changed successfully" });
    }

    [HttpGet("customers/{id}/addresses")]
    public async Task<IActionResult> GetAddresses(int id)
    {
        var data = await _svc.GetCustomerAddressesAsync(id);
        return Ok(new { success = true, data });
    }

    [HttpPost("customers/{id}/addresses")]
    public async Task<IActionResult> SaveAddress(int id, [FromBody] StorefrontSaveAddressDto dto)
    {
        var data = await _svc.CreateOrUpdateAddressAsync(id, dto);
        if (data == null) return NotFound(new { success = false, message = "Customer not found" });
        return Ok(new { success = true, data, message = "Address saved successfully" });
    }

    [HttpDelete("customers/{customerId}/addresses/{addressId}")]
    public async Task<IActionResult> DeleteAddress(int customerId, int addressId)
    {
        var ok = await _svc.DeleteAddressAsync(customerId, addressId);
        if (!ok) return NotFound(new { success = false, message = "Address not found" });
        return Ok(new { success = true, message = "Address deleted successfully" });
    }
}
