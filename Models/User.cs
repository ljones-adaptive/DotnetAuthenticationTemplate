namespace ScalpingApp.Models;

public enum UserRole { Admin, Trader, Monitor }

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Monitor;
    public bool IsApproved { get; set; } = false;
    public bool IsVerified { get; set; } = false;
    public bool IsSuspended { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => IsApproved && IsVerified && !IsSuspended;
}
