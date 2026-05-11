using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScalpingApp.Models;
using ScalpingApp.ViewModels;
using System.Security.Claims;

namespace ScalpingApp.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _db;
    public ProfileController(AppDbContext db) => _db = db;

    private User? CurrentUser() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? _db.Users.Find(id) : null;

    [HttpGet]
    public IActionResult Index()
    {
        var user = CurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");
        return View(BuildVm(user));
    }

    [HttpPost]
    public async Task<IActionResult> ChangeEmail(ChangeEmailViewModel vm)
    {
        var user = CurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");
        var result = BuildVm(user);

        if (!ModelState.IsValid)
        {
            result.EmailError = "A valid email address is required.";
            return View("Index", result);
        }
        if (_db.Users.Any(u => u.Email == vm.NewEmail && u.Id != user.Id))
        {
            result.EmailError = "That email is already in use.";
            return View("Index", result);
        }
        user.Email = vm.NewEmail;
        await _db.SaveChangesAsync();
        result.Email = vm.NewEmail;
        result.EmailSuccess = "Email updated successfully.";
        return View("Index", result);
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        var user = CurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");
        var result = BuildVm(user);

        if (!BCrypt.Net.BCrypt.Verify(vm.CurrentPassword, user.PasswordHash))
        {
            result.PasswordError = "Current password is incorrect.";
            return View("Index", result);
        }
        if (!ModelState.IsValid)
        {
            result.PasswordError = ModelState.Values
                .SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return View("Index", result);
        }
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.NewPassword);
        await _db.SaveChangesAsync();
        result.PasswordSuccess = "Password updated successfully.";
        return View("Index", result);
    }

    private static ProfileViewModel BuildVm(User user) => new()
    {
        Username = user.Username,
        Email = user.Email,
        Role = user.Role.ToString()
    };
}
