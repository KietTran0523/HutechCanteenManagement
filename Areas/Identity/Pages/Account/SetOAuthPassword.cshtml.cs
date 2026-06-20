#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace QuanLyCanTeenHutech.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class SetOAuthPasswordModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<SetOAuthPasswordModel> _logger;

    public SetOAuthPasswordModel(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        ILogger<SetOAuthPasswordModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} và tối đa {1} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }

        [Required]
        public string Code { get; set; }

        public string ReturnUrl { get; set; }
    }

    public IActionResult OnGet(string code = null, string email = null, string returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email))
            return BadRequest("Liên kết tạo mật khẩu không hợp lệ.");

        try
        {
            Input = new InputModel
            {
                Email = email,
                Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
                ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/")
            };
        }
        catch (FormatException)
        {
            return BadRequest("Liên kết tạo mật khẩu không hợp lệ.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Liên kết tạo mật khẩu không hợp lệ hoặc đã hết hạn.");
            return Page();
        }

        if (await _userManager.IsLockedOutAsync(user))
            return RedirectToPage("./Lockout");

        if (await _userManager.HasPasswordAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã có mật khẩu. Vui lòng đăng nhập.");
            return Page();
        }

        var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        var signInResult = await _signInManager.PasswordSignInAsync(
            user,
            Input.Password,
            isPersistent: false,
            lockoutOnFailure: false);

        if (!signInResult.Succeeded)
        {
            _logger.LogWarning("OAuth user {UserId} created a password but automatic sign-in failed.", user.Id);
            return RedirectToPage("./Login", new { ReturnUrl = SafeReturnUrl() });
        }

        _logger.LogInformation("OAuth user {UserId} created a local password and signed in.", user.Id);
        return LocalRedirect(SafeReturnUrl());
    }

    private string SafeReturnUrl() =>
        Url.IsLocalUrl(Input.ReturnUrl) ? Input.ReturnUrl : Url.Content("~/");
}
