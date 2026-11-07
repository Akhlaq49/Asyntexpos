namespace ReactPosApi.Services;

public interface ITenantMenuService
{
    Task<List<string>> GetHiddenMenuKeysAsync();
    Task<(bool success, int count)> UpdateHiddenMenuKeysAsync(string callerRole, List<string> hiddenKeys);
    Task<string> GetDefaultDashboardAsync();
    Task<bool> SetDefaultDashboardAsync(string callerRole, string dashboardPath);
}
