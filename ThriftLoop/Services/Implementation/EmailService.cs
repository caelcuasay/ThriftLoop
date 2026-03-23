using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ThriftLoop.Services.Email.Interface;

namespace ThriftLoop.Services.Email.Implementation;

/// <summary>
/// Sends transactional emails via SMTP using MailKit.
/// Requires NuGet: MailKit (Install-Package MailKit)
/// Configure the "Smtp" section in appsettings.json.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var smtp = _config.GetSection("Smtp");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            smtp["SenderName"] ?? "ThriftLoop",
            smtp["SenderEmail"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Reset your ThriftLoop password";

        message.Body = new BodyBuilder
        {
            HtmlBody = $"""
                <div style="font-family:sans-serif;max-width:500px;margin:auto;padding:24px">
                  <h2 style="color:#111827">Reset your password</h2>
                  <p style="color:#374151">
                    We received a request to reset the password for your ThriftLoop account
                    associated with this email address.
                  </p>
                  <p style="color:#374151">
                    Click the button below to choose a new password.
                    This link expires in <strong>1 hour</strong>.
                  </p>
                  <a href="{resetLink}"
                     style="display:inline-block;margin:16px 0;padding:12px 28px;
                            background:#4f46e5;color:#fff;border-radius:6px;
                            text-decoration:none;font-weight:600;font-size:15px">
                    Reset Password
                  </a>
                  <p style="color:#6b7280;font-size:13px;margin-top:24px;border-top:1px solid #e5e7eb;padding-top:16px">
                    If you didn't request a password reset you can safely ignore this email —
                    your password will not be changed.
                  </p>
                </div>
                """
        }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(
            smtp["Host"]!,
            int.Parse(smtp["Port"] ?? "587"),
            SecureSocketOptions.StartTls);

        await client.AuthenticateAsync(smtp["Username"]!, smtp["Password"]!);
        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);

        _logger.LogInformation("Password reset email sent to {Email}.", toEmail);
    }
}