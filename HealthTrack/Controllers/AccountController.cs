using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AccountController> logger) : Controller
{
    public static readonly string[] RegisterableRoles = [Roles.Client, Roles.Trainer, Roles.Professional];
    public static readonly string[] Genders = ["Male", "Female", "Other", "Prefer not to say"];

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Dashboard");

        return View(new RegisterViewModel());
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!RegisterableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Please choose a valid account type.");

        if (model.Gender is not null && !Genders.Contains(model.Gender))
            ModelState.AddModelError(nameof(model.Gender), "Please choose a valid option.");

        if (model.DateOfBirth is { } dob && (dob > DateOnly.FromDateTime(DateTime.Today) || dob.Year < 1900))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Please enter a valid date of birth.");

        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            LastLoginAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors.Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateUserName)))
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await userManager.AddToRoleAsync(user, model.Role);
        logger.LogInformation("New {Role} account registered: {UserId}", model.Role, user.Id);

        await signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = $"Welcome to HealthTrack, {user.FullName}!";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Dashboard");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var result = await signInManager.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (await userManager.FindByEmailAsync(model.Email.Trim()) is { } user)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await userManager.UpdateAsync(user);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut && await userManager.FindByEmailAsync(model.Email.Trim()) is { } locked
            && AccountStatus.IsDisabled(locked.LockoutEnd))
        {
            ModelState.AddModelError(string.Empty, "This account has been disabled. Please contact an administrator.");
        }
        else if (result.IsLockedOut)
        {
            logger.LogWarning("Account locked out after failed logins: {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Your account is locked for 15 minutes.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }

        return View(model);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        TempData["Success"] = "You have been signed out.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
