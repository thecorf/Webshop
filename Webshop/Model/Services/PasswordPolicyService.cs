namespace Webshop.Model.Services;
public sealed class PasswordPolicyService
{
    private readonly PasswordBlocklist _blocklist;

    public PasswordPolicyService(PasswordBlocklist blocklist)
        => _blocklist = blocklist;

    public const int MinimumLength = 8;

    /// <summary>Returnerer fejlbeskeder — tom liste = gyldig adgangskode.</summary>
    public List<string> Validate(string password, string username, string email)
    {
        var errors = new List<string>();

        if (password.Length < MinimumLength)
            errors.Add($"Adgangskoden skal være mindst {MinimumLength} tegn.");
        if (!password.Any(char.IsDigit))
            errors.Add("Adgangskoden skal indeholde mindst ét tal.");
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            errors.Add("Adgangskoden skal indeholde mindst ét specialtegn.");
        if (password.Contains(username, StringComparison.OrdinalIgnoreCase) && username.Length > 0)
            errors.Add("Adgangskoden må ikke indeholde dit brugernavn.");
        if (password.Contains(email, StringComparison.OrdinalIgnoreCase) && email.Length > 0)
            errors.Add("Adgangskoden må ikke indeholde din e-mail.");
        if (_blocklist.IsBreached(password))
            errors.Add("Adgangskoden er for almindelig — vælg en mere unik adgangskode.");

        return errors;
    }


    public static int ScoreStrength(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        var score = 0;

        if (password.Length >= 8) score++;
        if (password.Length >= 12) score++;
        if (password.Length >= 16) score++;

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));
        var classes = new[] { hasLower, hasUpper, hasDigit, hasSpecial }.Count(b => b);
        if (classes >= 3) score++;

        if (IsSequential(password) || HasRepeatedRuns(password))
            score -= 2;

        return Math.Clamp(score, 0, 4);
    }

    private static bool IsSequential(string password)
    {
        var sequence = 1;
        for (var i = 1; i < password.Length; i++)
        {
            var delta = password[i] - password[i - 1];
            sequence = (delta == 1 || delta == -1) ? sequence + 1 : 1;
            if (sequence >= 4) return true;
        }
        return false;
    }

    private static bool HasRepeatedRuns(string password)
    {
        var run = 1;
        for (var i = 1; i < password.Length; i++)
        {
            run = password[i] == password[i - 1] ? run + 1 : 1;
            if (run >= 4) return true;
        }
        return false;
    }
}