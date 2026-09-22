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

public class RegisterModel : PageModel
{
    private const int MinLength = 8;

    private readonly IUserRepository _userRepository;
    private readonly ISessionService _sessionService;
    private readonly PasswordBlocklist _blocklist;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(IUserRepository userRepository,
                         ISessionService sessionService,
                         PasswordBlocklist blocklist,
                         ILogger<RegisterModel> logger)
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
        _blocklist = blocklist;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Brugernavn er påkrævet.")]
        [StringLength(200, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_.\-]+$",
            ErrorMessage = "Brugernavn må kun indeholde bogstaver, tal, '.', '_' og '-'.")]
        public string UserName { get; set; } = "";

        [Required(ErrorMessage = "Fulde navn er påkrævet.")]
        [StringLength(200)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "E-mail er påkrævet.")]
        [EmailAddress]
        [StringLength(320)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Adgangskode er påkrævet.")]
        [StringLength(64, ErrorMessage = "Adgangskoden må maksimalt være 64 tegn.")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Bekræft venligst din adgangskode.")]
        [Compare("Password", ErrorMessage = "Adgangskoder matcher ikke.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var existingUser = await _userRepository.GetByUsernameAsync(Input.UserName);
        if (existingUser != null)
        {
            ModelState.AddModelError("UserName", "Brugernavnet er allerede i brug.");
            return Page();
        }

        var existingEmail = await _userRepository.GetByEmailAsync(Input.Email);
        if (existingEmail != null)
        {
            ModelState.AddModelError("Email", "E-mail-addresset er allerede i brug.");
            return Page();
        }

        if (Input.Password.Length < MinLength)
        {
            ModelState.AddModelError("Password", $"Adgangskoden skal være mindst {MinLength} tegn.");
            return Page();
        }

        if (!HasDigit(Input.Password))
        {
            ModelState.AddModelError("Password", "Adgangskoden skal indeholde mindst ét tal.");
            return Page();
        }

        if (!HasSpecialChar(Input.Password))
        {
            ModelState.AddModelError("Password", "Adgangskoden skal indeholde mindst ét specialtegn.");
            return Page();
        }

        if (Input.Password.Contains(Input.UserName, StringComparison.OrdinalIgnoreCase) ||
            Input.Password.Contains(Input.Email.Split('@')[0], StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Password", "Adgangskoden må ikke indeholde brugernavn eller email.");
            return Page();
        }

        if (_blocklist.IsBreached(Input.Password))
        {
            ModelState.AddModelError("Password", "Adgangskoden findes på kendte lister over lækkede kodeord.");
            return Page();
        }

        var newUser = new User
        {
            UserName = Input.UserName,
            FullName = Input.FullName,
            Email = Input.Email,
            Role = Webshop.Model.User.UserRole.Customer
        };

        try
        {
            var createdUser = await _userRepository.CreateUserAsync(newUser, Input.Password);

            _logger.LogInformation("Ny bruger '{UserName}' registreret med e-mail {Email}",
                createdUser.UserName, createdUser.Email);
            await AutoSignIn(createdUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fejl ved registrering af bruger '{UserName}'", Input.UserName);
            ModelState.AddModelError(string.Empty,
                "Der opstod en fejl under oprettelsen. Prøv venligst igen.");
            return Page();
        }

        return RedirectToPage("/Login");
    }

    private static bool HasDigit(string password) => password.Any(char.IsDigit);
    private static bool HasSpecialChar(string password) => password.Any(c => char.IsPunctuation(c) || char.IsSymbol(c));

    private async Task AutoSignIn(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role == Webshop.Model.User.UserRole.Admin ? "Admin" : "Customer")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        _sessionService.SetUserInSession(HttpContext, user.Id, user.UserName, user.Role);
    }
}