using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace QuanLyCanTeenHutech.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý tài khoản";
        ViewData["ActivePage"] = "Users";

        var users = await _userManager.Users.ToListAsync();
        var userWithRoles = new List<UserRolesViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userWithRoles.Add(new UserRolesViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Roles = roles.ToList()
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
        var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

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

        var currentRoles = await _userManager.GetRolesAsync(user);
        
        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        if (!string.IsNullOrEmpty(selectedRole))
        {
            await _userManager.AddToRoleAsync(user, selectedRole);
        }

        TempData["SuccessMessage"] = $"Cập nhật quyền cho tài khoản {user.UserName} thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Prevent admin from deleting themselves
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == user.Id)
        {
            TempData["ErrorMessage"] = "Bạn không thể xóa chính tài khoản của mình đang đăng nhập!";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"Đã xóa tài khoản {user.UserName} thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = "Xóa tài khoản thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Index));
    }
}

public class UserRolesViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
