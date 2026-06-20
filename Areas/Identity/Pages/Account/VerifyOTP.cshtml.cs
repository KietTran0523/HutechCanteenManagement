using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QuanLyCanTeenHutech.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class VerifyOTPModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        public VerifyOTPModel(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
            [Display(Name = "Mã OTP")]
            public string OTP { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập Mật khẩu mới")]
            [StringLength(100, ErrorMessage = "{0} phải dài từ {2} đến {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu mới")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận Mật khẩu mới")]
            [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToPage("./ForgotPassword");
            }

            Input = new InputModel
            {
                Email = email
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Show generic error
                ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ hoặc đã hết hạn.");
                return Page();
            }

            var isOtpValid = await _userManager.VerifyTwoFactorTokenAsync(user, "Email", Input.OTP);
            if (!isOtpValid)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ hoặc đã hết hạn.");
                return Page();
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, Input.NewPassword);

            if (result.Succeeded)
            {
                // Provide a success message and redirect
                TempData["SuccessMessage"] = "Mật khẩu của bạn đã được đặt lại thành công.";
                return RedirectToPage("./Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }
}
