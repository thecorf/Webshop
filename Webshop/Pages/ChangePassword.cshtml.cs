using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model;
using Webshop.Model.Repos;
using Webshop.Model.Services;

namespace Webshop.Pages;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordBlocklist _blocklist;
    private readonly ILogger<ChangePasswordModel> _logger;

    public ChangePasswordModel(IUserRepository userRepository,
                                   PasswordBlocklist blocklist,
                                   ILogger<ChangePasswordModel> logger)
    {
        _userRepository = userRepository;
        _blocklist = blocklist;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Indtast din nuværende adgangskode.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Indtast en ny adgangskode.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Gentag den nye adgangskode.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "De to adgangskoder matcher ikke.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        var userId = GetCurrentUserId();
        if (userId is null)
            return Challenge();

        var user = await _userRepository.GetUserByIdAsync(userId.Value, ct);
        if (user is null)
            return Challenge();

        // 1. Verificér nuværende adgangskode mod hash i DB.
        //    Generisk fejlbesked — afslører ikke OM brugernavnet eller koden var forkert.
        if (!Argon2.Verify(user.PasswordHash, Input.CurrentPassword))
        {
            _logger.LogWarning("Fejlet skift af adgangskode (forkert nuværende kode) for bruger {UserId}", userId.Value);
            ModelState.AddModelError(string.Empty, "Din nuværende adgangskode er forkert.");
            return Page();
        }

        // 2. Kør HELE password-politikken igen — skift må ikke være en genvej udenom reglerne
        var policyError = ValidatePasswordPolicy(Input.NewPassword, user.UserName, user.Email);
        if (policyError is not null)
        {
            ModelState.AddModelError("Input.NewPassword", policyError);
            return Page();
        }

        // 3. Genbrugs-tjek: ny kode må ikke være den gamle
        if (Argon2.Verify(user.PasswordHash, Input.NewPassword))
        {
            ModelState.AddModelError("Input.NewPassword",
                "Den nye adgangskode må ikke være identisk med den nuværende.");
            return Page();
        }

        // 4. Gem (Argon2.Hash sker inde i repository'et)
        var updated = await _userRepository.UpdatePasswordAsync(userId.Value, Input.NewPassword, ct);
        if (!updated)
        {
            _logger.LogError("Databaseopdatering fejlede ved skift af adgangskode for {UserId}", userId.Value);
            ModelState.AddModelError(string.Empty, "Adgangskoden kunne ikke opdateres. Prøv igen.");
            return Page();
        }

        _logger.LogInformation("Bruger {UserId} skiftede adgangskode", userId.Value);

        // 5. Ryd session og cookie, og kræv re-login med den nye kode
        HttpContext.Session.Clear();
        TempData["StatusMessage"] = "Din adgangskode er ændret. Log ind med din nye adgangskode.";
        return SignOut(new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = "/Login"
        });
    }

    private string? ValidatePasswordPolicy(string password, string username, string email)
    {
        if (password.Length < 8)
            return "Adgangskoden skal være mindst 8 tegn.";
        if (!password.Any(char.IsDigit))
            return "Adgangskoden skal indeholde mindst ét tal.";
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            return "Adgangskoden skal indeholde mindst ét specialtegn.";
        if (password.Contains(username, StringComparison.OrdinalIgnoreCase))
            return "Adgangskoden må ikke indeholde dit brugernavn.";
        if (password.Contains(email, StringComparison.OrdinalIgnoreCase))
            return "Adgangskoden må ikke indeholde din e-mail.";
        if (_blocklist.IsBreached(password))
            return "Adgangskoden er for almindelig — vælg en mere unik adgangskode.";
        return null;
    }

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : null;
    }
}