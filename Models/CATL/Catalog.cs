namespace ReactPosApi.Models;

// Product catalogue, per company (Database/CatalogSchema.sql has the tables, keys and views).
//   category = SolutionGroup, family = SolutionProductLine, product = CatalogProduct, SKU = Product (inventory row)
// These tables are really deleted (no soft delete): their unique names and foreign keys stay simple.

/// <summary>A plan: Standard, Premium. Code is what the drawing reader is asked for.</summary>
public class CatalogTier : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>A family (SolutionProductLine) is allowed in a tier</summary>
public class CatalogFamilyTier : ITenantEntity
{
    public int TenantId { get; set; }
    public int FamilyId { get; set; }
    public int TierId { get; set; }
}

public class CatalogColor : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>One product: the price-list name without colour. Its SKUs are the Products rows that point at it.</summary>
public class CatalogProduct : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>pc, or m for rails (package quantities may be fractional)</summary>
    public string Unit { get; set; } = "pc";
    /// <summary>Packages-file price, used while the product is not on the price list</summary>
    public decimal? FallbackPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Another name of a product or a family (packages / categorization file, drawings) - for matching only</summary>
public class CatalogAlias : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string AliasKey { get; set; } = string.Empty;
    public string Source { get; set; } = "manual";
    /// <summary>product / family</summary>
    public string TargetKind { get; set; } = "product";
    public int? CatalogProductId { get; set; }
    public int? FamilyId { get; set; }
}

/// <summary>Quantity of a product in a package (building size) for a tier. No row = not in that package.</summary>
public class CatalogPackageLine : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int PackageSizeId { get; set; }
    public int TierId { get; set; }
    public int CatalogProductId { get; set; }
    public decimal Quantity { get; set; }
}

/// <summary>A product a drawing rule (the reader's rules file) uses; Rank 1 is tried first; Gang '' = any</summary>
public class CatalogDrawingRuleItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Gang { get; set; } = string.Empty;
    public int Rank { get; set; } = 1;
    public int CatalogProductId { get; set; }
}

/// <summary>Where the columns are in a company's file (pricelist / categorization / packages), as JSON</summary>
public class CatalogImportProfile : ITenantEntity
{
    public int TenantId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ProfileJson { get; set; } = "{}";
}

/// <summary>An imported name that matched nothing: waits for a person to map it; RowJson is applied then</summary>
public class CatalogUnmatched : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public string RowJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
