namespace Webshop.Model;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Argon2-hash (salt + parametre indlejret). Må aldrig indeholde klartekst.</summary>
    public string PasswordHash { get; set; } = string.Empty; // Argon2-hash
    public UserRole Role { get; set; }

    public enum UserRole
    {
        Admin,
        Customer
    }
}
