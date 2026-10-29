using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

// Smart home solution data used by the package calculation engine (Inventory > Solution Packages).
//  - Product categorization: groups (Touch Panel, Smart Access ...) and their product lines,
//    each marked for the Standard and/or the Premium solution.
//  - Packages: how many of each item a building of each size needs (1 BHK ... 10 Kanal).

/// <summary>A heading of the product categorization (Touch Panel, Lighting Control ...)</summary>
public class SolutionGroup : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<SolutionProductLine> ProductLines { get; set; } = new List<SolutionProductLine>();
}

/// <summary>A product of the categorization (Hpad Echo, Piano Series ...) and the solutions it is used in</summary>
public class SolutionProductLine : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int GroupId { get; set; }
    public SolutionGroup? Group { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Used in the Standard solution</summary>
    public bool InStandard { get; set; }
    /// <summary>Used in the Premium solution</summary>
    public bool InPremium { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A building size a package is made for (1 BHK, 5 Marla, 1 Kanal ...) - a column of the packages sheet</summary>
public class PackageSize : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

/// <summary>An item of the packages sheet (Piano Switch 3 Gang, Curtain Rail 1 meter ...)</summary>
public class PackageItem : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Its line in the product categorization - tells whether it belongs to the Standard or the Premium solution</summary>
    public int? ProductLineId { get; set; }
    public SolutionProductLine? ProductLine { get; set; }

    /// <summary>The inventory product it is sold as (optional; for prices and stock in the calculation)</summary>
    public int? ProductId { get; set; }

    public int SortOrder { get; set; }

    public ICollection<PackageQuantity> Quantities { get; set; } = new List<PackageQuantity>();
}

/// <summary>How many of an item a building of a size needs (a cell of the packages sheet; no row = 0)</summary>
public class PackageQuantity : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int PackageItemId { get; set; }
    public PackageItem? PackageItem { get; set; }

    public int PackageSizeId { get; set; }
    public PackageSize? PackageSize { get; set; }

    public int Quantity { get; set; }
}
