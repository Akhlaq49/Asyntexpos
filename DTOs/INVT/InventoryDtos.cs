namespace ReactPosApi.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CreatedOn { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string SellingType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary>The catalogue product this SKU is a colour of (its siblings are the other colours)</summary>
    public int? CatalogProductId { get; set; }
    public bool IsDefaultSku { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string BarcodeSymbology { get; set; } = string.Empty;
    public string ItemBarcode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductType { get; set; } = "single";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string TaxType { get; set; } = string.Empty;
    public string Tax { get; set; } = string.Empty;
    public List<AppliedTaxDto> Taxes { get; set; } = new();
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public int QuantityAlert { get; set; }
    public string Warranty { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string ManufacturedDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string[] Images { get; set; } = Array.Empty<string>();
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public class WarrantyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string Period { get; set; } = "Month";
    public string DurationDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class CreateWarrantyDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string Period { get; set; } = "Month";
    public string Status { get; set; } = "active";
}

public class UnitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public string Status { get; set; } = "active";
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateUnitDto
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class BrandDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string Status { get; set; } = "active";
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateBrandDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}
