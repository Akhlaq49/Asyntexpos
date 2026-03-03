namespace ReactPosApi.Services;

/// <summary>
/// Provides the current tenant ID from the authenticated user's JWT claims.
/// Registered as scoped service — resolved once per HTTP request.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// The current tenant's ID. Returns 0 if no tenant context (e.g., unauthenticated).
    /// </summary>
    int TenantId { get; }
}
