using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace QuanLyCanTeenHutech.Controllers;

public class LanguageController : Controller
{
    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );

        // Safely redirect back to the return URL or home page
        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
