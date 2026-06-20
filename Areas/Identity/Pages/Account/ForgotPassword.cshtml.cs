using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QuanLyCanTeenHutech.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<IdentityUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập Email")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    // Actually, if we don't require confirmed account, maybe we should just allow it.
                    if (user != null)
                    {
                        var code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
                        
                        var subject = "Mã OTP khôi phục mật khẩu";
                        var message = $"Chào bạn,<br/><br/>Bạn vừa yêu cầu khôi phục mật khẩu trên hệ thống Hutech Canteen Management.<br/><br/>Mã OTP của bạn là: <b>{code}</b><br/><br/>Mã này có hiệu lực trong vòng 3 phút.<br/><br/>Trân trọng,<br/>Ban Quản Trị Hutech Canteen";
                        
                        await _emailSender.SendEmailAsync(Input.Email, subject, message);
                        
                        return RedirectToPage("./VerifyOTP", new { email = Input.Email });
                    }
                    else 
                    {
                        // To prevent email enumeration, redirect to VerifyOTP anyway or show a generic message
                        return RedirectToPage("./VerifyOTP", new { email = Input.Email });
                    }
                }

                var otpCode = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
                        
                var otpSubject = "Mã OTP khôi phục mật khẩu";
                var otpMessage = $"Chào bạn,<br/><br/>Bạn vừa yêu cầu khôi phục mật khẩu trên hệ thống Hutech Canteen Management.<br/><br/>Mã OTP của bạn là: <b style=\"font-size:20px; color:#d32f2f;\">{otpCode}</b><br/><br/>Mã này có hiệu lực trong vòng vài phút.<br/><br/>Trân trọng,<br/>Ban Quản Trị Hutech Canteen";
                
                await _emailSender.SendEmailAsync(Input.Email, otpSubject, otpMessage);
                
                return RedirectToPage("./VerifyOTP", new { email = Input.Email });
            }

            return Page();
        }
    }
}
