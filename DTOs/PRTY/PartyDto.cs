namespace ReactPosApi.DTOs;

public class PartyDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneWork { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Picture { get; set; }
    public string? Code { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? UserName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class CreatePartyDto
{
    public string FullName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneWork { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Code { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
}
