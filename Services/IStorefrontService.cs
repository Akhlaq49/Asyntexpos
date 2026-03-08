namespace ReactPosApi.Services;

using ReactPosApi.DTOs;

public interface IStorefrontService
{
    // Categories
    Task<List<StorefrontCategoryDto>> GetAllCategoriesAsync();
    Task<StorefrontCategoryDto?> GetCategoryByIdAsync(int id);

    // Products
    Task<List<StorefrontProductDto>> GetAllProductsAsync();
    Task<StorefrontProductDto?> GetProductByIdAsync(int id);
    Task<List<StorefrontProductDto>> GetProductsByCategoryAsync(int categoryId);
    Task<List<StorefrontProductDto>> GetProductsBySubCategoryAsync(int subCategoryId);
    Task<List<StorefrontProductDto>> SearchProductsAsync(string query);

    // Orders
    Task<StorefrontOrderResultDto> CreateOrderAsync(StorefrontOrderDto dto);
    Task<List<StorefrontOrderSummaryDto>> GetOrdersByEmailAsync(string email);
    Task<StorefrontOrderDetailDto?> GetOrderByIdAsync(int id);
    Task<StorefrontOrderDetailDto?> GetOrderByOrderNumberAsync(string orderNumber);

    // Auth
    Task<StorefrontAuthResultDto> RegisterAsync(StorefrontRegisterDto dto);
    Task<StorefrontAuthResultDto> LoginAsync(StorefrontLoginDto dto);

    // Customer / Account
    Task<StorefrontCustomerDto?> GetCustomerByEmailAsync(string email);
    Task<StorefrontCustomerDto?> GetCustomerByIdAsync(int id);
    Task<StorefrontCustomerDto?> UpdateCustomerAsync(int customerId, StorefrontUpdateCustomerDto dto);
    Task<bool> ChangePasswordAsync(int customerId, StorefrontChangePasswordDto dto);
    Task<List<StorefrontAddressResultDto>> GetCustomerAddressesAsync(int customerId);
    Task<StorefrontAddressResultDto?> CreateOrUpdateAddressAsync(int customerId, StorefrontSaveAddressDto dto);
    Task<bool> DeleteAddressAsync(int customerId, int addressId);
}
