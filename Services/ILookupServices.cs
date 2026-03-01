using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IStoreService
{
    Task<List<DropdownOptionDto>> GetAllAsync();
}

public interface IWarehouseService
{
    Task<List<DropdownOptionDto>> GetAllAsync();
}
