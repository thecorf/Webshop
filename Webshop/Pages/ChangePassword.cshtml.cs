using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model.Repos;
using Webshop.Model.Services;

namespace Webshop.Pages;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordPolicyService _passwordPolicy;
    private readonly ILogger<ChangePasswordModel> _logger;

    public ChangePasswordModel(IUserRepository userRepository,
                               PasswordPolicyService passwordPolicy,
                               ILogger<ChangePasswordModel> logger)
    {
        _userRepository = userRepository;
        _passwordPolicy = passwordPolicy;
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

    public IActionResult OnGet()
    {
        if (IsAdmin()) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (IsAdmin()) return NotFound();

        if (!ModelState.IsValid)
            return Page();

        var userId = GetCurrentUserId();
        if (userId is null)
            return Challenge();

        var user = await _userRepository.GetUserByIdAsync(userId.Value, ct);
        if (user is null)
            return Challenge();

        if (!Argon2.Verify(user.PasswordHash, Input.CurrentPassword))
        {
            _logger.LogWarning("Fejlet skift af adgangskode (forkert nuværende kode) for bruger {UserId}",
                userId.Value);
            ModelState.AddModelError(string.Empty, "Din nuværende adgangskode er forkert.");
            return Page();
        }

        var policyErrors = _passwordPolicy.Validate(Input.NewPassword, user.UserName, user.Email);
        foreach (var error in policyErrors)
            ModelState.AddModelError("Input.NewPassword", error);

        if (policyErrors.Count > 0)
            return Page();

        if (Argon2.Verify(user.PasswordHash, Input.NewPassword))
        {
            ModelState.AddModelError("Input.NewPassword",
                "Den nye adgangskode må ikke være identisk med den nuværende.");
            return Page();
        }

        var updated = await _userRepository.UpdatePasswordAsync(userId.Value, Input.NewPassword, ct);
        if (!updated)
        {
            _logger.LogError("Databaseopdatering fejlede ved skift af adgangskode for {UserId}", userId.Value);
            ModelState.AddModelError(string.Empty, "Adgangskoden kunne ikke opdateres. Prøv igen.");
            return Page();
        }

        _logger.LogInformation("Bruger {UserId} skiftede adgangskode", userId.Value);

        HttpContext.Session.Clear();
        TempData["StatusMessage"] = "Din adgangskode er ændret. Log ind med din nye adgangskode.";
        return SignOut(new AuthenticationProperties { RedirectUri = "/Login" });
    }

    private bool IsAdmin()
    {
        if (!User.IsInRole("Admin"))
            return false;

        var userId = GetCurrentUserId();
        _logger.LogWarning("ADMIN blokeret fra password-skift: {UserId}", userId);
        return true;
    }

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : null;
    }
}