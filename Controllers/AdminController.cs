using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScalpingApp.Models;
using ScalpingApp.Services;
using System.Security.Claims;

namespace ScalpingApp.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;

    public AdminController(AppDbContext db, ITokenService tokens,
        IEmailService email, IConfiguration config)
    {
        _db = db; _tokens = tokens; _email = email; _config = config;
    }

    private string BaseUrl => _config["AppSettings:BaseUrl"] ?? "https://scalping.adaptiverealtimetrading.co.uk";
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet]
    public IActionResult Users() => View(_db.Users.OrderBy(u => u.CreatedAt).ToList());

    [HttpPost]
    public async Task<IActionResult> ChangeRole(int uid, string role)
    {
        if (uid == CurrentUserId()) return RedirectToAction("Users");
        var user = await _db.Users.FindAsync(uid);
        if (user != null && Enum.TryParse<UserRole>(role, out var parsed))
        {
            user.Role = parsed;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Users");
    }

    [HttpPost]
    public async Task<IActionResult> Suspend(int uid)
    {
        if (uid == CurrentUserId()) return RedirectToAction("Users");
        var user = await _db.Users.FindAsync(uid);
        if (user != null)
        {
            user.IsSuspended = !user.IsSuspended;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Users");
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int uid)
    {
        if (uid == CurrentUserId()) return RedirectToAction("Users");
        var user = await _db.Users.FindAsync(uid);
        if (user != null)
        {
            user.IsSuspended = true;
            await _db.SaveChangesAsync();
            var tok = _tokens.Generate(user.Id.ToString(), "pw-reset");
            await _email.SendPasswordResetAsync(user.Email, user.Username,
                $"{BaseUrl}/Account/ResetPassword/{tok}");
        }
        return RedirectToAction("Users");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int uid)
    {
        if (uid == CurrentUserId()) return RedirectToAction("Users");
        var user = await _db.Users.FindAsync(uid);
        if (user != null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Users");
    }
}
