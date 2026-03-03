using System.Security.Claims;

namespace ReactPosApi.Services;

/// <summary>
/// Reads the TenantId from the current HTTP request:
/// 1. JWT "TenantId" claim (authenticated admin/user requests)
/// 2. X-Tenant-Id header (anonymous storefront requests)
/// </summary>
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public int TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return 0;

            // 1. Try JWT claim first (authenticated requests)
            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var claim = user.FindFirst("TenantId");
                if (claim != null && int.TryParse(claim.Value, out var jwtTenantId))
                    return jwtTenantId;
            }

            // 2. Fallback: X-Tenant-Id header (for anonymous storefront requests)
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValues))
            {
                if (int.TryParse(headerValues.FirstOrDefault(), out var headerTenantId))
                    return headerTenantId;
            }

            return 0;
        }
    }
}
