using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IFormFieldConfigService _formFieldConfigService;

    public AuthService(AppDbContext db, IConfiguration config, IFormFieldConfigService formFieldConfigService)
    {
        _db = db;
        _config = config;
        _formFieldConfigService = formFieldConfigService;
    }

    private static readonly string[] LoginRoles = { "SuperAdmin", "Admin", "Manager", "User" };

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("Email and password are required.");

        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new ArgumentException("Full name is required.");

        if (dto.Password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");

        // Bypass tenant query filter — email must be globally unique for auth
        var exists = await _db.Parties
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Email == dto.Email.ToLower());
        if (exists)
            throw new InvalidOperationException("Email is already registered.");

        // ── MULTI-TENANCY: Registration creates a SuperAdmin with a new tenant ──
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var tenant = new Tenant
            {
                Name = dto.FullName.Trim(),
                Email = dto.Email.ToLower().Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(); // Get the tenant Id

            var party = new Party
            {
                FullName = dto.FullName.Trim(),
                Email = dto.Email.ToLower().Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "SuperAdmin",  // Registering user becomes SuperAdmin (tenant owner)
                IsActive = true,
                TenantId = tenant.Id,  // Explicitly set — bypass auto-set since no JWT yet
                CreatedAt = DateTime.UtcNow
            };

            _db.Parties.Add(party);
            await _db.SaveChangesAsync();

            // Seed default form field configs for the new tenant
            await _formFieldConfigService.SeedDefaultsAsync(tenant.Id);

            await transaction.CommitAsync();

            var token = GenerateJwtToken(party);
            return new AuthResponseDto { Token = token, User = MapToDto(party) };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("Email and password are required.");

        // Bypass tenant query filter — login must search across all tenants
        var party = await _db.Parties
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Email == dto.Email.ToLower()
                                   && LoginRoles.Contains(p.Role)
                                   && p.PasswordHash != null);
        if (party == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, party.PasswordHash!))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var token = GenerateJwtToken(party);
        return new AuthResponseDto { Token = token, User = MapToDto(party) };
    }

    public async Task<UserDto?> GetCurrentUserAsync(int userId)
    {
        var party = await _db.Parties.FindAsync(userId);
        return party == null ? null : MapToDto(party);
    }

    private string GenerateJwtToken(Party party)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, party.Id.ToString()),
            new Claim(ClaimTypes.Email, party.Email ?? ""),
            new Claim(ClaimTypes.Name, party.FullName),
            new Claim(ClaimTypes.Role, party.Role),
            new Claim("TenantId", party.TenantId.ToString())  // ← embedded for all subsequent requests
        };

        var expiryMinutes = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "1440");

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserDto MapToDto(Party p) => new()
    {
        Id = p.Id,
        FullName = p.FullName,
        Email = p.Email ?? "",
        Phone = p.Phone ?? "",
        Role = p.Role,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt
    };

    private static readonly string[] AdminRoles = { "SuperAdmin", "Admin" };

    public async Task<bool> VerifyAdminPasswordAsync(int tenantId, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        var admins = await _db.Parties
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId
                     && AdminRoles.Contains(p.Role)
                     && p.PasswordHash != null)
            .ToListAsync();

        return admins.Any(a => BCrypt.Net.BCrypt.Verify(password, a.PasswordHash!));
    }
}
