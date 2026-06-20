// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace QuanLyCanTeenHutech.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ChangePasswordModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mật khẩu cũ.")]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu cũ")]
            public string OldPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không tìm thấy tài khoản.");
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToPage("./SetPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không tìm thấy tài khoản.");
            }

            var checkPassword = await _userManager.CheckPasswordAsync(user, Input.OldPassword);
            if (!checkPassword)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu cũ không đúng.");
                return Page();
            }

            var email = await _userManager.GetEmailAsync(user);
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Tài khoản chưa có email để xác nhận đổi mật khẩu.");
                return Page();
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    code = code,
                    email = email
                },
                protocol: Request.Scheme);

            var emailHtml = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 24px; border: 1px solid #eee; border-radius: 10px;'>
    <h2 style='color: #dc3545;'>Xác nhận đổi mật khẩu HutechCanteen</h2>

    <p>Xin chào,</p>

    <p>Bạn vừa yêu cầu đổi mật khẩu tài khoản HutechCanteen.</p>

    <p>Vui lòng bấm nút bên dưới để xác nhận và nhập mật khẩu mới:</p>

    <p style='margin: 30px 0;'>
        <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'
           style='background: #dc3545; color: white; padding: 12px 20px; text-decoration: none; border-radius: 6px;'>
           Xác nhận đổi mật khẩu
        </a>
    </p>

    <p style='color: #666; font-size: 13px;'>
        Nếu bạn không yêu cầu đổi mật khẩu, hãy bỏ qua email này.
    </p>
</div>";

            await _emailSender.SendEmailAsync(
                email,
                "Xác nhận đổi mật khẩu HutechCanteen",
                emailHtml);

            StatusMessage = "Đã gửi email xác nhận đổi mật khẩu. Vui lòng kiểm tra hộp thư.";
            return RedirectToPage();
        }
    }
}
