using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace QuanLyCanTeenHutech.Hubs;

/// <summary>
/// Re-checks account lockout state for established SignalR connections.
/// Cookie validation only runs during the handshake, so without this filter a
/// connection opened before an admin lock could otherwise keep invoking Hub methods.
/// </summary>
public sealed class AccountAccessHubFilter : IHubFilter
{
    private readonly UserManager<IdentityUser> _userManager;

    public AccountAccessHubFilter(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        if (!await HasAccessAsync(context.Context))
        {
            context.Context.Abort();
            return;
        }

        await next(context);
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!await HasAccessAsync(invocationContext.Context))
        {
            invocationContext.Context.Abort();
            throw new HubException("Tài khoản đã bị khóa hoặc vô hiệu hóa.");
        }

        return await next(invocationContext);
    }

    private async Task<bool> HasAccessAsync(HubCallerContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true) return false;

        var user = await _userManager.GetUserAsync(context.User);
        return user != null && !await _userManager.IsLockedOutAsync(user);
    }
}
