using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IFormFieldConfigService _formFieldConfigService;

    /// <summary>
    /// Must stay in sync with <c>Users.tsx</c> role dropdown plus <c>SuperAdmin</c> (tenant owners).
    /// </summary>
    private static readonly string[] UserRoles =
    {
        "SuperAdmin", "Admin", "Manager", "Salesman", "Supervisor",
        "Store Keeper", "Delivery Biker", "Maintenance", "Quality Analyst",
        "Accountant", "Purchase", "User"
    };

    private static bool IsUserRole(string? role)
        => !string.IsNullOrWhiteSpace(role)
           && UserRoles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);

    public UserService(AppDbContext db, IFormFieldConfigService formFieldConfigService)
    {
        _db = db;
        _formFieldConfigService = formFieldConfigService;
    }

    public async Task<PagedResult<UserDto>> GetAllPagedAsync(PaginationQuery query)
    {
        // Filter by valid user roles (roles are normalized during create/update, so exact match is safe)
        var q = _db.Parties
            .Where(p => p.Role != null && UserRoles.Contains(p.Role))
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(p => p.FullName.ToLower().Contains(s) ||
                             (p.Email != null && p.Email.ToLower().Contains(s)) ||
                             (p.Phone != null && p.Phone.Contains(s)));
        }

        q = q.OrderByDescending(p => p.CreatedAt);

        var totalCount = await q.CountAsync();
        var entities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new UserDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Email = p.Email ?? "",
                Phone = p.Phone ?? "",
                Role = p.Role,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<UserDto>
        {
            Items = entities,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        return await _db.Parties
            .IgnoreQueryFilters()
            .Where(p => p.Role != null && UserRoles.Contains(p.Role))
            .Where(p => p.TenantId == _db.CurrentTenantId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new UserDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Email = p.Email ?? "",
                Phone = p.Phone ?? "",
                Role = p.Role,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        var p = await _db.Parties.FindAsync(id);
        if (p == null || !IsUserRole(p.Role)) return null;
        return MapToDto(p);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new ArgumentException("Full name is required.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");

        // Bypass tenant filter — emails must be globally unique for login-capable roles
        var exists = await _db.Parties
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Email == dto.Email.ToLower().Trim());
        if (exists)
            throw new InvalidOperationException("Email is already registered.");

        var role = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role.Trim();

        // Validate role is in the allowed list
        if (!IsUserRole(role))
            throw new ArgumentException($"Invalid role: {role}. Must be one of: {string.Join(", ", UserRoles)}");

        // Normalize role to match exact casing from UserRoles array
        role = UserRoles.First(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));

        var party = new Party
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email.ToLower().Trim(),
            Phone = dto.Phone?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = role,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        // ── MULTI-TENANCY: If SuperAdmin, create a new tenant for this user ──
        if (role == "SuperAdmin")
        {
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
                await _db.SaveChangesAsync(); // Get tenant Id

                party.TenantId = tenant.Id; // Explicitly assign new tenant
                _db.Parties.Add(party);
                await _db.SaveChangesAsync();

                // Seed default form field configs for the new tenant
                await _formFieldConfigService.SeedDefaultsAsync(tenant.Id);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            // Non-SuperAdmin users inherit TenantId from current context (auto-set by SaveChanges)
            _db.Parties.Add(party);
            await _db.SaveChangesAsync();
        }

        return MapToDto(party);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto)
    {
        var party = await _db.Parties.FindAsync(id);
        if (party == null || !IsUserRole(party.Role)) return null;

        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new ArgumentException("Full name is required.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");

        var emailLower = dto.Email.ToLower().Trim();
        if (emailLower != party.Email)
        {
            // Bypass tenant filter — emails must be globally unique for login-capable roles
            var exists = await _db.Parties
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Email == emailLower && p.Id != id);
            if (exists)
                throw new InvalidOperationException("Email is already registered.");
        }

        party.FullName = dto.FullName.Trim();
        party.Email = emailLower;
        party.Phone = dto.Phone?.Trim();
        var newRole = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role.Trim();

        // Validate role is in the allowed list
        if (!IsUserRole(newRole))
            throw new ArgumentException($"Invalid role: {newRole}. Must be one of: {string.Join(", ", UserRoles)}");

        // Normalize role to match exact casing from UserRoles array
        newRole = UserRoles.First(r => r.Equals(newRole, StringComparison.OrdinalIgnoreCase));

        party.IsActive = dto.IsActive;

        // If promoting to SuperAdmin and user doesn't already own a tenant, create one
        if (newRole == "SuperAdmin" && party.Role != "SuperAdmin")
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var tenant = new Tenant
                {
                    Name = dto.FullName.Trim(),
                    Email = emailLower,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Tenants.Add(tenant);
                await _db.SaveChangesAsync();

                party.TenantId = tenant.Id;
                await _formFieldConfigService.SeedDefaultsAsync(tenant.Id);

                party.Role = newRole;

                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    if (dto.Password.Length < 6)
                        throw new ArgumentException("Password must be at least 6 characters.");
                    party.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return MapToDto(party);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        party.Role = newRole;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            if (dto.Password.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters.");
            party.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        await _db.SaveChangesAsync();
        return MapToDto(party);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var party = await _db.Parties.FindAsync(id);
        if (party == null || !IsUserRole(party.Role)) return false;
        _db.Parties.Remove(party);
        await _db.SaveChangesAsync();
        return true;
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
}
