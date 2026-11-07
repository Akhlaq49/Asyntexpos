using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync();
    Task<PagedResult<UserDto>> GetAllPagedAsync(PaginationQuery query);
    Task<UserDto?> GetByIdAsync(int id);
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto);
    Task<bool> DeleteAsync(int id);
    /// <summary>Freeze (block sign-in) or unfreeze a user; null when the user isn't found</summary>
    Task<UserDto?> SetFrozenAsync(int id, bool frozen, int actorId, string actorRole, string actorName);
}
