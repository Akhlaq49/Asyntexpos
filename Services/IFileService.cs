using Microsoft.AspNetCore.Http;

namespace ReactPosApi.Services;

public interface IFileService
{
    Task<string> SaveFileAsync(IFormFile file, string folder);
}
