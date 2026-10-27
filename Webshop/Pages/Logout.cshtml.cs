using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Webshop.Pages;

public class LogoutModel : PageModel
{
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(ILogger<LogoutModel> logger)
    {
        _logger = logger;
    }

    public IActionResult OnPost()
    {
        _logger.LogInformation("Bruger {User} loggede ud",
            HttpContext.User.Identity?.Name ?? "ukendt");

        // Tøm session FØR signout — kurv og alt andet state ryddes,
        // så en evt. ny bruger på samme browser ikke arver kurven
        HttpContext.Session.Clear();

        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}