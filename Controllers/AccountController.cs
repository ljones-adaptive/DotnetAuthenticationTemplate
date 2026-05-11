using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ScalpingApp.Models;
using ScalpingApp.Services;
using ScalpingApp.ViewModels;
using System.Security.Claims;

namespace ScalpingApp.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;

    public AccountController(AppDbContext db, ITokenService tokens,
        IEmailService email, IConfiguration config)
    {
        _db = db; _tokens = tokens; _email = email; _config = config;
    }

    private string BaseUrl => _config["AppSettings:BaseUrl"] ?? "https://scalping.adaptiverealtimetrading.co.uk";

    // ── Login ───────────────────────────────────────────────────────────────

    [HttpGet] public IActionResult Login() =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new LoginViewModel());

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        var user = _db.Users.FirstOrDefault(u => u.Username == vm.Username);
        if (user != null && BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            if (user.IsSuspended)      { vm.Error = "suspended";            return View(vm); }
            if (!user.IsApproved)      { vm.Error = "pending_approval";     return View(vm); }
            if (!user.IsVerified)      { vm.Error = "pending_verification"; return View(vm); }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new("role", user.Role.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
            return RedirectToAction("Index", "Home");
        }
        vm.Error = "invalid";
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // ── Register ────────────────────────────────────────────────────────────

    [HttpGet] public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (_db.Users.Any(u => u.Username == vm.Username))
            ModelState.AddModelError(nameof(vm.Username), "Username already taken.");
        if (_db.Users.Any(u => u.Email == vm.Email))
            ModelState.AddModelError(nameof(vm.Email), "Email already registered.");

        if (!ModelState.IsValid) return View(vm);

        var firstUser = !_db.Users.Any();
        var user = new User
        {
            Username = vm.Username,
            Email = vm.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            Role = firstUser ? UserRole.Admin : UserRole.Monitor,
            IsApproved = firstUser,
            IsVerified = firstUser
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (firstUser) return View("RegisterSuccess", "first_admin");

        var approveTok = _tokens.Generate($"approve:{user.Id}", "admin-action");
        var denyTok = _tokens.Generate($"deny:{user.Id}", "admin-action");
        foreach (var admin in _db.Users.Where(u => u.Role == UserRole.Admin))
        {
            await _email.SendApprovalRequestAsync(admin.Email, user.Username, user.Email,
                $"{BaseUrl}/Account/AdminAction/{approveTok}",
                $"{BaseUrl}/Account/AdminAction/{denyTok}");
        }
        return View("RegisterSuccess", "pending");
    }

    // ── Admin action (approve / deny) ───────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> AdminAction(string id)
    {
        var data = _tokens.Verify(id, "admin-action");
        if (data == null) return View("Message", new MessageViewModel
            { Title = "Link Expired", Message = "This link is invalid or has expired." });

        var parts = data.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var uid))
            return View("Message", new MessageViewModel { Title = "Invalid Link", Message = "Malformed token." });

        var user = await _db.Users.FindAsync(uid);
        if (user == null) return View("Message", new MessageViewModel { Title = "Not Found", Message = "User not found." });

        if (parts[0] == "approve")
        {
            if (user.IsApproved) return View("Message", new MessageViewModel
                { Title = "Already Approved", Message = $"{user.Username} has already been approved." });
            user.IsApproved = true;
            await _db.SaveChangesAsync();
            var tok = _tokens.Generate(user.Id.ToString(), "email-verify");
            await _email.SendVerificationAsync(user.Email, user.Username, $"{BaseUrl}/Account/Verify/{tok}");
            return View("Message", new MessageViewModel
                { Title = "User Approved", Message = $"{user.Username} approved. Verification email sent." });
        }
        else
        {
            var name = user.Username;
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return View("Message", new MessageViewModel
                { Title = "Registration Denied", Message = $"Registration for {name} has been denied." });
        }
    }

    // ── Email verification ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Verify(string id)
    {
        var data = _tokens.Verify(id, "email-verify");
        if (data == null) return View("Message", new MessageViewModel
            { Title = "Link Expired", Message = "This verification link is invalid or has expired." });

        if (!int.TryParse(data, out var uid)) return View("Message",
            new MessageViewModel { Title = "Invalid Link", Message = "Malformed token." });

        var user = await _db.Users.FindAsync(uid);
        if (user == null) return View("Message", new MessageViewModel { Title = "Not Found", Message = "User not found." });

        user.IsVerified = true;
        await _db.SaveChangesAsync();
        return View("Message", new MessageViewModel
            { Title = "Email Verified", Message = "Your email has been verified. You can now log in.", ShowLogin = true });
    }

    // ── Forgotten username ──────────────────────────────────────────────────

    [HttpGet] public IActionResult ForgotUsername() => View(new ForgotEmailViewModel());

    [HttpPost]
    public async Task<IActionResult> ForgotUsername(ForgotEmailViewModel vm)
    {
        if (ModelState.IsValid)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == vm.Email);
            if (user != null)
                await _email.SendUsernameReminderAsync(user.Email, user.Username, BaseUrl);
            vm.Sent = true;
        }
        return View(vm);
    }

    // ── Forgotten password ──────────────────────────────────────────────────

    [HttpGet] public IActionResult ForgotPassword() => View(new ForgotEmailViewModel());

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotEmailViewModel vm)
    {
        if (ModelState.IsValid)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == vm.Email);
            if (user != null)
            {
                user.IsSuspended = true;
                await _db.SaveChangesAsync();
                var tok = _tokens.Generate(user.Id.ToString(), "pw-reset");
                await _email.SendPasswordResetAsync(user.Email, user.Username,
                    $"{BaseUrl}/Account/ResetPassword/{tok}");
            }
            vm.Sent = true;
        }
        return View(vm);
    }

    // ── Reset password ──────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult ResetPassword(string id) =>
        View(new ResetPasswordViewModel { Token = id });

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        var data = _tokens.Verify(vm.Token, "pw-reset", 3600);
        if (data == null) return View("Message", new MessageViewModel
            { Title = "Link Expired", Message = "This password reset link is invalid or has expired." });

        if (!int.TryParse(data, out var uid)) return View("Message",
            new MessageViewModel { Title = "Invalid Link", Message = "Malformed token." });

        var user = await _db.Users.FindAsync(uid);
        if (user == null) return View("Message", new MessageViewModel { Title = "Not Found", Message = "User not found." });

        if (!ModelState.IsValid) return View(vm);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.NewPassword);
        user.IsSuspended = false;
        await _db.SaveChangesAsync();
        return View("Message", new MessageViewModel
            { Title = "Password Reset", Message = "Your password has been reset. You can now log in.", ShowLogin = true });
    }
}
