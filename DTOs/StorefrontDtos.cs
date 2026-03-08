namespace ReactPosApi.DTOs;

// ── Category DTOs ──

public class StorefrontCategoryDto
{
    public string Id { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string CategorySlug { get; set; } = "";
    public bool IsActive { get; set; }
    public List<StorefrontSubCategoryDto> SubCategories { get; set; } = new();
}

public class StorefrontSubCategoryDto
{
    public string Id { get; set; } = "";
    public string SubCategoryName { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public bool IsActive { get; set; }
    public List<object> SubCategory1s { get; set; } = new();
}

// ── Product DTOs ──

public class StorefrontProductDto
{
    public string Id { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal Price { get; set; }
    public string Description { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Stock { get; set; }
    public string? ImagePath { get; set; }
    public string[] ImagePaths { get; set; } = Array.Empty<string>();
    public string? CategoryId { get; set; }
    public string? SubCategoryId { get; set; }
    public StorefrontProductCategoryRef Category { get; set; } = new();
    public StorefrontProductBrandRef Brand { get; set; } = new();
    public string Unit { get; set; } = "";
    public string DiscountType { get; set; } = "";
    public decimal DiscountValue { get; set; }
    public string CreatedOn { get; set; } = "";
}

public class StorefrontProductCategoryRef
{
    public string Id { get; set; } = "";
    public string CategoryName { get; set; } = "";
}

public class StorefrontProductBrandRef
{
    public string BrandName { get; set; } = "";
}

// ── Order DTOs ──

public class StorefrontOrderDto
{
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public StorefrontAddressDto? BillingAddress { get; set; }
    public StorefrontAddressDto? ShippingAddress { get; set; }
    public List<StorefrontOrderItemDto> Items { get; set; } = new();
    public string? PaymentMethod { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Shipping { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public string? AdditionalInfo { get; set; }
    public bool CreateAccount { get; set; }
    public string? AccountPassword { get; set; }
}

public class StorefrontOrderItemDto
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
}

public class StorefrontAddressDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class StorefrontOrderResultDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
}

public class StorefrontOrderSummaryDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string OrderSource { get; set; } = "Online";
    public string PaymentStatus { get; set; } = "";
    public string OrderDate { get; set; } = "";
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class StorefrontOrderDetailDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Shipping { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public string? Notes { get; set; }
    public string CreatedAt { get; set; } = "";
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }
    public List<StorefrontOrderItemDetailDto> Items { get; set; } = new();
}

public class StorefrontOrderItemDetailDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductSku { get; set; }
    public string? ImagePath { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalCost { get; set; }
}

// ── Auth DTOs ──

public class StorefrontRegisterDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class StorefrontLoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class StorefrontAuthResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; } = 200;
    public StorefrontCustomerDto? Customer { get; set; }
}

// ── Customer / Account DTOs ──

public class StorefrontCustomerDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
}

public class StorefrontUpdateCustomerDto
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
}

public class StorefrontChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class StorefrontSaveAddressDto
{
    public int? AddressId { get; set; }
    public string AddressType { get; set; } = "Billing";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
}

public class StorefrontAddressResultDto
{
    public int Id { get; set; }
    public string AddressType { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
}
