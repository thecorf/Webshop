using System;
using System.Collections.Generic;
using System.IO;

namespace Webshop.Model.Services;

public sealed class PasswordBlocklist
{
    private const string DefaultFileName = "breached_passwords.txt";
    private readonly HashSet<string> _breachedPasswords;

    public PasswordBlocklist(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path must be provided.", nameof(filePath));
        _breachedPasswords = new HashSet<string>(StringComparer.Ordinal);
        Load(filePath);
    }

    public PasswordBlocklist()
        : this(Path.Combine(AppContext.BaseDirectory, DefaultFileName))
    {
    }

    public bool IsBreached(string password)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        return _breachedPasswords.Contains(password);
    }

    private void Load(string filePath)
    {
        if (!File.Exists(filePath)) return;
        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            _breachedPasswords.Add(trimmed);
        }
    }
}
