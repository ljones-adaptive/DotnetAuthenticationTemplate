using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace ScalpingApp.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task SendAsync(string to, string subject, string html)
    {
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Scalping | ART", _config["Mail:Username"]));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;
            msg.Body = new TextPart(TextFormat.Html) { Text = html };

            using var client = new SmtpClient();
            await client.ConnectAsync(_config["Mail:Server"],
                int.Parse(_config["Mail:Port"] ?? "587"),
                SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_config["Mail:Username"], _config["Mail:Password"]);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }

    private static string Wrap(string body) => $"""
        <!DOCTYPE html><html><head><meta charset="UTF-8"></head>
        <body style="font-family:-apple-system,sans-serif;background:#f4f4f4;padding:2rem;margin:0">
          <div style="max-width:480px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.1)">
            <div style="background:#0a0e1a;padding:1.5rem 2rem">
              <h1 style="color:#00d4ff;font-size:1rem;margin:0;letter-spacing:0.5px">Scalping | Adaptive Realtime Trading</h1>
            </div>
            <div style="padding:2rem">{body}</div>
          </div>
        </body></html>
        """;

    private static string Btn(string url, string label, string bg = "#00d4ff", string color = "#0a0e1a") =>
        $"""<a href="{url}" style="display:inline-block;padding:0.75rem 2rem;background:{bg};color:{color};border-radius:8px;text-decoration:none;font-weight:700;font-size:0.9rem">{label}</a>""";

    public Task SendApprovalRequestAsync(string adminEmail, string newUser, string newEmail, string approveUrl, string denyUrl) =>
        SendAsync(adminEmail, $"New registration: {newUser}", Wrap($"""
            <h2 style="font-size:1rem;color:#111;margin:0 0 1rem">New User Registration</h2>
            <p style="color:#555;font-size:0.9rem;margin:0 0 1rem">A new user has requested access:</p>
            <div style="background:#f8f8f8;border-radius:8px;padding:1rem;margin:0 0 1.5rem">
              <p style="margin:0 0 0.25rem;font-size:0.875rem;color:#333"><strong>Username:</strong> {newUser}</p>
              <p style="margin:0;font-size:0.875rem;color:#333"><strong>Email:</strong> {newEmail}</p>
            </div>
            {Btn(approveUrl, "Approve", "#10b981", "#fff")}
            <span style="display:inline-block;width:1rem"></span>
            {Btn(denyUrl, "Deny", "#ef4444", "#fff")}
            <p style="color:#999;font-size:0.75rem;margin-top:1.5rem">This link expires in 24 hours.</p>
            """));

    public Task SendVerificationAsync(string userEmail, string username, string verifyUrl) =>
        SendAsync(userEmail, "Verify your email – Adaptive Realtime Trading", Wrap($"""
            <h2 style="font-size:1rem;color:#111;margin:0 0 1rem">Verify Your Email</h2>
            <p style="color:#555;font-size:0.9rem;margin:0 0 1.5rem">Hi <strong>{username}</strong>, your registration has been approved. Please verify your email to activate your account.</p>
            {Btn(verifyUrl, "Verify Email")}
            <p style="color:#999;font-size:0.75rem;margin-top:1.5rem">This link expires in 24 hours.</p>
            """));

    public Task SendUsernameReminderAsync(string userEmail, string username, string loginUrl) =>
        SendAsync(userEmail, "Your username – Adaptive Realtime Trading", Wrap($"""
            <h2 style="font-size:1rem;color:#111;margin:0 0 1rem">Your Username</h2>
            <div style="background:#f8f8f8;border-radius:8px;padding:1rem;margin:0 0 1.5rem;text-align:center">
              <span style="font-size:1.4rem;font-weight:700;color:#0a0e1a;letter-spacing:1px">{username}</span>
            </div>
            {Btn(loginUrl, "Log In")}
            <p style="color:#999;font-size:0.75rem;margin-top:1.5rem">If you did not request this, ignore this email.</p>
            """));

    public Task SendPasswordResetAsync(string userEmail, string username, string resetUrl) =>
        SendAsync(userEmail, "Password reset – Adaptive Realtime Trading", Wrap($"""
            <h2 style="font-size:1rem;color:#111;margin:0 0 1rem">Password Reset</h2>
            <p style="color:#555;font-size:0.9rem;margin:0 0 1.5rem">Hi <strong>{username}</strong>, click below to reset your password. Your account has been suspended until you complete this step.</p>
            {Btn(resetUrl, "Reset Password")}
            <p style="color:#999;font-size:0.75rem;margin-top:1.5rem">This link expires in 1 hour. If you did not request this, ignore this email.</p>
            """));
}
