using ReactPosApi.Models;

namespace ReactPosApi.Services;

public interface IFormFieldConfigService
{
    Task<List<FormFieldConfig>> GetAllAsync();
    Task<List<FormFieldConfig>> GetByFormAsync(string formName);
    Task<FormFieldConfig?> GetByIdAsync(int id);
    Task<FormFieldConfig> UpsertAsync(string formName, string fieldName, string fieldLabel, bool isVisible);
    Task<List<FormFieldConfig>> BulkUpsertAsync(List<FormFieldConfig> configs);
    Task<bool> DeleteAsync(int id);
    Task SeedDefaultsAsync();
}
