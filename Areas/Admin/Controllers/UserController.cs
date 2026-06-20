using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Hubs;
using QuanLyCanTeenHutech.Services;

namespace QuanLyCanTeenHutech.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private const string AccountStatusClaimType = "account_status";
    private const string SoftDeletedStatus = "soft_deleted";
    private static readonly DateTimeOffset PermanentLockoutEnd = DateTimeOffset.MaxValue;

    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<UserController> _logger;

    public UserController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IHubContext<ChatHub> hubContext,
        ILogger<UserController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý tài khoản";
        ViewData["ActivePage"] = "Users";

        var users = await _userManager.Users.OrderBy(user => user.UserName).ToListAsync();
        var userWithRoles = new List<UserRolesViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var isSoftDeleted = claims.Any(IsSoftDeletedClaim);

            userWithRoles.Add(new UserRolesViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Roles = roles.ToList(),
                IsSoftDeleted = isSoftDeleted,
                IsLocked = !isSoftDeleted && await _userManager.IsLockedOutAsync(user)
            });
        }

        return View(userWithRoles);
    }

    public async Task<IActionResult> EditRoles(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var userRoles = await _userManager.GetRolesAsync(user);
        var allRoles = await _roleManager.Roles.Select(role => role.Name).ToListAsync();

        ViewData["Title"] = "Cấu hình vai trò";
        ViewData["ActivePage"] = "Users";
        ViewBag.AllRoles = allRoles;
        ViewBag.UserRoles = userRoles;

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoles(string id, string selectedRole)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (await IsSoftDeletedAsync(user))
        {
            TempData["ErrorMessage"] = "Hãy khôi phục tài khoản trước khi thay đổi vai trò.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
            return RedirectWithErrors("Không thể cập nhật vai trò", removeResult);

        if (!string.IsNullOrEmpty(selectedRole))
        {
            var addResult = await _userManager.AddToRoleAsync(user, selectedRole);
            if (!addResult.Succeeded)
                return RedirectWithErrors("Không thể cập nhật vai trò", addResult);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        TempData["SuccessMessage"] = $"Đã cập nhật quyền cho tài khoản {user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string id)
    {
        var user = await FindManageableUserAsync(id, "khóa");
        if (user == null) return RedirectToAction(nameof(Index));

        if (await IsSoftDeletedAsync(user))
        {
            TempData["ErrorMessage"] = "Tài khoản này đã được xóa mềm.";
            return RedirectToAction(nameof(Index));
        }

        var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
        if (!enableResult.Succeeded) return RedirectWithErrors("Khóa tài khoản thất bại", enableResult);

        var lockResult = await _userManager.SetLockoutEndDateAsync(user, PermanentLockoutEnd);
        if (!lockResult.Succeeded) return RedirectWithErrors("Khóa tài khoản thất bại", lockResult);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            return RedirectWithErrors("Khóa tài khoản thất bại", stampResult);
        }

        await NotifyAccessRevokedAsync(user.Id, "Tài khoản của bạn vừa bị quản trị viên khóa.");
        TempData["SuccessMessage"] = $"Đã khóa tài khoản {user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (await IsSoftDeletedAsync(user))
        {
            TempData["ErrorMessage"] = "Tài khoản đã xóa mềm chỉ có thể mở lại bằng chức năng khôi phục.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded) return RedirectWithErrors("Mở khóa tài khoản thất bại", result);

        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        TempData["SuccessMessage"] = $"Đã mở khóa tài khoản {user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(string id)
    {
        var user = await FindManageableUserAsync(id, "xóa mềm");
        if (user == null) return RedirectToAction(nameof(Index));

        if (await IsSoftDeletedAsync(user))
        {
            TempData["ErrorMessage"] = "Tài khoản này đã được xóa mềm trước đó.";
            return RedirectToAction(nameof(Index));
        }

        var claim = new Claim(AccountStatusClaimType, SoftDeletedStatus);
        var claimResult = await _userManager.AddClaimAsync(user, claim);
        if (!claimResult.Succeeded) return RedirectWithErrors("Xóa mềm tài khoản thất bại", claimResult);

        var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
        var lockResult = enableResult.Succeeded
            ? await _userManager.SetLockoutEndDateAsync(user, PermanentLockoutEnd)
            : enableResult;

        if (!lockResult.Succeeded)
        {
            await _userManager.RemoveClaimAsync(user, claim);
            return RedirectWithErrors("Xóa mềm tài khoản thất bại", lockResult);
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.RemoveClaimAsync(user, claim);
            return RedirectWithErrors("Xóa mềm tài khoản thất bại", stampResult);
        }

        await NotifyAccessRevokedAsync(user.Id, "Tài khoản của bạn vừa bị quản trị viên vô hiệu hóa.");
        TempData["SuccessMessage"] = $"Đã xóa mềm tài khoản {user.UserName}. Dữ liệu đơn hàng và chat được giữ nguyên.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var claims = (await _userManager.GetClaimsAsync(user)).Where(IsSoftDeletedClaim).ToList();
        if (claims.Count == 0)
        {
            TempData["ErrorMessage"] = "Tài khoản này không ở trạng thái xóa mềm.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var claim in claims)
        {
            var removeResult = await _userManager.RemoveClaimAsync(user, claim);
            if (!removeResult.Succeeded) return RedirectWithErrors("Khôi phục tài khoản thất bại", removeResult);
        }

        var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!unlockResult.Succeeded) return RedirectWithErrors("Khôi phục tài khoản thất bại", unlockResult);

        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        TempData["SuccessMessage"] = $"Đã khôi phục tài khoản {user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    // Backward-compatible endpoint: old delete forms now perform a safe soft delete.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(string id) => SoftDelete(id);

    private async Task<IdentityUser?> FindManageableUserAsync(string id, string actionName)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản.";
            return null;
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == user.Id)
        {
            TempData["ErrorMessage"] = $"Bạn không thể {actionName} chính tài khoản đang đăng nhập.";
            return null;
        }

        return user;
    }

    private async Task<bool> IsSoftDeletedAsync(IdentityUser user) =>
        (await _userManager.GetClaimsAsync(user)).Any(IsSoftDeletedClaim);

    private static bool IsSoftDeletedClaim(Claim claim) =>
        claim.Type == AccountStatusClaimType && claim.Value == SoftDeletedStatus;

    private IActionResult RedirectWithErrors(string message, IdentityResult result)
    {
        TempData["ErrorMessage"] = $"{message}: {string.Join(", ", result.Errors.Select(error => error.Description))}";
        return RedirectToAction(nameof(Index));
    }

    private async Task NotifyAccessRevokedAsync(string userId, string message)
    {
        try
        {
            await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(userId))
                .SendAsync("AccountAccessRevoked", new { message });
        }
        catch (Exception exception)
        {
            // The account is already secured by lockout + security-stamp rotation.
            // A transient notification failure must not turn a successful lock into HTTP 500.
            _logger.LogWarning(exception, "Could not notify locked user {UserId} through SignalR.", userId);
        }
    }
}

public class UserRolesViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsLocked { get; set; }
    public bool IsSoftDeleted { get; set; }
}
