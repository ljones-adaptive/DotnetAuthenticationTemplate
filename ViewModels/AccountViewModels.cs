using System.ComponentModel.DataAnnotations;

namespace ScalpingApp.ViewModels;

public class LoginViewModel
{
    [Required] public string Username { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public string? Error { get; set; }
}

public class RegisterViewModel
{
    [Required] public string Username { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ForgotEmailViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    public bool Sent { get; set; }
}

public class ResetPasswordViewModel
{
    public string Token { get; set; } = "";
    [Required] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangeEmailViewModel
{
    [Required, EmailAddress] public string NewEmail { get; set; } = "";
}

public class ChangePasswordViewModel
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ProfileViewModel
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public ChangeEmailViewModel ChangeEmail { get; set; } = new();
    public ChangePasswordViewModel ChangePassword { get; set; } = new();
    public string? EmailSuccess { get; set; }
    public string? EmailError { get; set; }
    public string? PasswordSuccess { get; set; }
    public string? PasswordError { get; set; }
}

public class MessageViewModel
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public bool ShowLogin { get; set; }
}
