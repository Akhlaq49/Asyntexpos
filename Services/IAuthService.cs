using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<UserDto?> GetCurrentUserAsync(int userId);
}
