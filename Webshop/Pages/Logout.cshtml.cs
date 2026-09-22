using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model.Services;

namespace Webshop.Pages;

public class LogoutModel : PageModel
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(ISessionService sessionService, ILogger<LogoutModel> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Sign ud af cookie-auth FØR sessionen røres
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Total session-rydning: alle data væk ved logout
        HttpContext.Session.Clear();
        _sessionService.ClearSession(HttpContext);

        _logger.LogInformation("Bruger logget ud. UserId var {UserId}",
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);

        return RedirectToPage("/Index");
    }
}