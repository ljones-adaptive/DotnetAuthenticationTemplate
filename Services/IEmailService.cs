namespace ScalpingApp.Services;

public interface IEmailService
{
    Task SendApprovalRequestAsync(string adminEmail, string newUser, string newEmail, string approveUrl, string denyUrl);
    Task SendVerificationAsync(string userEmail, string username, string verifyUrl);
    Task SendUsernameReminderAsync(string userEmail, string username, string loginUrl);
    Task SendPasswordResetAsync(string userEmail, string username, string resetUrl);
}
