namespace ReactPosApi.DTOs;

public class CustomerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SO { get; set; }
    public string? Cnic { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? BuildingType { get; set; }
    public string? BuildingSpace { get; set; }
    public string? BuildingStage { get; set; }
    public string? Picture { get; set; }
    public string Status { get; set; } = "active";
}

/// <summary>Name and phone are required; every other field is optional</summary>
public class CreateCustomerDto
{
    public string? BuildingType { get; set; }
    public string? BuildingSpace { get; set; }
    public string? BuildingStage { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SO { get; set; }
    public string? Cnic { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string Status { get; set; } = "active";
}
