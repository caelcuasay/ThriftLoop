namespace ThriftLoop.Services.Email.Interface;

public interface IEmailService
{
    /// <summary>
    /// Sends a password-reset email containing <paramref name="resetLink"/> to the user.
    /// </summary>
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
}