// UserProfileViewComponent.cs
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Webshop.Pages.Shared.Components.UserProfile;

public class UserProfileViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var user = HttpContext.User;
        var isLoggedIn = user.Identity?.IsAuthenticated ?? false;
        var userName = isLoggedIn ? (user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name) : null;
        var isAdmin = user.IsInRole("Admin");

        return View(new UserModel(isLoggedIn, userName, isAdmin));
    }
}