using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private static readonly string[] UserRoles = { "Admin", "Manager", "User" };

    public UserService(AppDbContext db) => _db = db;

    public async Task<PagedResult<UserDto>> GetAllPagedAsync(PaginationQuery query)
    {
        var q = _db.Parties.Where(p => UserRoles.Contains(p.Role)).AsQueryable();

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
            .Where(p => UserRoles.Contains(p.Role))
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
        if (p == null || !UserRoles.Contains(p.Role)) return null;
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

        var exists = await _db.Parties.AnyAsync(p => p.Email == dto.Email.ToLower().Trim());
        if (exists)
            throw new InvalidOperationException("Email is already registered.");

        var party = new Party
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email.ToLower().Trim(),
            Phone = dto.Phone?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Parties.Add(party);
        await _db.SaveChangesAsync();
        return MapToDto(party);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto)
    {
        var party = await _db.Parties.FindAsync(id);
        if (party == null || !UserRoles.Contains(party.Role)) return null;

        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new ArgumentException("Full name is required.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");

        var emailLower = dto.Email.ToLower().Trim();
        if (emailLower != party.Email)
        {
            var exists = await _db.Parties.AnyAsync(p => p.Email == emailLower && p.Id != id);
            if (exists)
                throw new InvalidOperationException("Email is already registered.");
        }

        party.FullName = dto.FullName.Trim();
        party.Email = emailLower;
        party.Phone = dto.Phone?.Trim();
        party.Role = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role.Trim();
        party.IsActive = dto.IsActive;

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
        if (party == null || !UserRoles.Contains(party.Role)) return false;
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
