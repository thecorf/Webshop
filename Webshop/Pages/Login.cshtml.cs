using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Webshop.Model;
using Webshop.Model.Repos;
using Webshop.Model.Services;

namespace Webshop.Pages;

public class LoginModel : PageModel
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionService _sessionService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IUserRepository userRepository,
                      ISessionService sessionService,
                      ILogger<LoginModel> logger)
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Brugernavn er påkrævet.")]
        [StringLength(200)]
        public string UserName { get; set; } = "";

        [Required(ErrorMessage = "Adgangskode er påkrævet.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userRepository.GetByUsernameAsync(Input.UserName);

        // Generisk fejlbesked (ingen username enumeration)
        if (user is null || !Argon2.Verify(user.PasswordHash, Input.Password))
        {
            _logger.LogWarning("Fejlet loginforsøg for '{UserName}' fra IP {Ip}",
                Input.UserName, HttpContext.Connection.RemoteIpAddress);

            ModelState.AddModelError(string.Empty, "Ugyldigt brugernavn eller adgangskode.");
            return Page();
        }

        // Session rotation (session fixation protection)
        HttpContext.Session.Clear();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, ((Webshop.Model.User.UserRole)user.Role).ToString()),
            new Claim(ClaimTypes.GivenName,user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?? user.UserName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        // Session-data (minimalt: kun Id, Username, Role)
        _sessionService.SetUserInSession(HttpContext, user.Id, user.UserName, user.Role);

        _logger.LogInformation("Bruger '{UserName}' logget ind fra IP {Ip}",
            user.UserName, HttpContext.Connection.RemoteIpAddress);

        return RedirectToPage("/Index");
    }
}