namespace ThriftLoop.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Null for users who registered exclusively via an external provider (e.g. Google).
    /// </summary>
    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}