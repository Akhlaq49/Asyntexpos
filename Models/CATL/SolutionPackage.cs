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

/// <summary>A family of the categorization (Hpad Echo, Piano Series ...); its plans (Standard / Premium) are in CatalogFamilyTiers</summary>
public class SolutionProductLine : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int GroupId { get; set; }
    public SolutionGroup? Group { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

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
