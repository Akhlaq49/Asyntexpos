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
}
