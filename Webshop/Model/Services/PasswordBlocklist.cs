using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Webshop.Model.Services;

public sealed class PasswordBlocklist
{
    private readonly HashSet<string> _breachedPasswords;
    private readonly ILogger<PasswordBlocklist> _logger;

    public PasswordBlocklist(ILogger<PasswordBlocklist> logger, IHostEnvironment env)
    {
        _logger = logger;
        _breachedPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var filePath = Path.Combine(env.ContentRootPath, "Model", "Services", "password_blocklist.txt");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Password blocklist-fil ikke fundet: {FilePath}", filePath);
            return;
        }

        try
        {
            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    _breachedPasswords.Add(trimmed);
            }

            _logger.LogInformation("Password blocklist indlæst: {Count} poster", _breachedPasswords.Count);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Kunne ikke læse password blocklist-fil");
        }
    }

    /// <summary>
    /// Returnerer true hvis adgangskoden findes i blokeringslisten.
    /// </summary>
    public bool IsBreached(string password)
        => _breachedPasswords.Contains(password);
}