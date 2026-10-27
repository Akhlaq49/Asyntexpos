using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;

namespace ReactPosApi.Services;

/// <summary>Sign-in refused: an admin has frozen this account</summary>
public class AccountFrozenException : Exception
{
    public const string Code = "ACCOUNT_FROZEN";
    public const string UserMessage = "Your account has been restricted by the administration.";
    public AccountFrozenException() : base(UserMessage) { }
}

/// <summary>
/// Signs out a user who is frozen while already logged in: every request with their token
/// gets 401 + code ACCOUNT_FROZEN, so the app returns to the sign-in page with the message.
/// </summary>
public class FrozenAccountMiddleware
{
    private readonly RequestDelegate _next;
    public FrozenAccountMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var frozen = await db.Parties.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.Id == userId).Select(p => p.IsFrozen).FirstOrDefaultAsync();
            if (frozen)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = AccountFrozenException.UserMessage, code = AccountFrozenException.Code });
                return;
            }
        }
        await _next(context);
    }
}
