using System.Text;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace HealthTrack.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AccountEmails accountEmails,
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
        await SendConfirmationAsync(user);

        await signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = $"Welcome to HealthTrack, {user.FullName}! We've sent a confirmation link to {user.Email}.";
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

    [HttpGet, Authorize]
    public async Task<IActionResult> Manage()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        return View(await BuildManageAsync(user, new ProfileFormViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender
        }));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileFormViewModel form)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        if (form.Gender is not null && !Genders.Contains(form.Gender))
            ModelState.AddModelError("Profile.Gender", "Please choose a valid option.");
        if (form.DateOfBirth is { } dob && (dob > DateOnly.FromDateTime(DateTime.Today) || dob.Year < 1900))
            ModelState.AddModelError("Profile.DateOfBirth", "Please enter a valid date of birth.");

        var email = form.Email.Trim();
        var emailChanged = !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged && ModelState.IsValid
            && (string.IsNullOrEmpty(form.CurrentPassword) || !await userManager.CheckPasswordAsync(user, form.CurrentPassword)))
            ModelState.AddModelError("Profile.CurrentPassword", "Enter your current password to change your email.");

        if (!ModelState.IsValid)
            return View(nameof(Manage), await BuildManageAsync(user, form));

        var oldEmail = user.Email;
        user.FullName = form.FullName.Trim();
        user.DateOfBirth = form.DateOfBirth;
        user.Gender = form.Gender;
        if (emailChanged)
        {
            user.Email = email;
            user.UserName = email;
            user.EmailConfirmed = false;
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors.Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateUserName)))
                ModelState.AddModelError(error.Code.Contains("Email") ? "Profile.Email" : string.Empty, error.Description);
            return View(nameof(Manage), await BuildManageAsync(user, form));
        }

        if (emailChanged)
        {
            logger.LogInformation("User {UserId} changed their email", user.Id);
            await SendConfirmationAsync(user);
            if (oldEmail is not null)
                accountEmails.SendEmailChanged(user, oldEmail, email);
        }
        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = emailChanged
            ? $"Your profile has been updated. Check {email} for a link to confirm your new address."
            : "Your profile has been updated.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordViewModel form)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        if (ModelState.IsValid)
        {
            var result = await userManager.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
            if (result.Succeeded)
            {
                await signInManager.RefreshSignInAsync(user);
                logger.LogInformation("User {UserId} changed their password", user.Id);
                if (user.EmailConfirmed)
                    accountEmails.SendPasswordChanged(user);
                TempData["Success"] = "Your password has been changed.";
                return RedirectToAction(nameof(Manage));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(
                    error.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? "Password.CurrentPassword" : "Password.NewPassword",
                    error.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? "Your current password is incorrect." : error.Description);
        }

        return View(nameof(Manage), await BuildManageAsync(user, new ProfileFormViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender
        }, form));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotifications([Bind(Prefix = "Notifications")] NotificationSettingsViewModel form)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        user.EmailAppointments = form.EmailAppointments;
        user.EmailReminders = form.EmailReminders;
        user.EmailUpdates = form.EmailUpdates;
        user.RemindWorkouts = form.RemindWorkouts;
        user.RemindMeals = form.RemindMeals;
        await userManager.UpdateAsync(user);

        TempData["Success"] = "Notification settings saved.";
        return RedirectToAction(nameof(Manage), null, "notifications");
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        var confirmed = user is not null && code is not null && await userManager.ConfirmEmailAsync(user, Decode(code)) is { Succeeded: true };
        if (confirmed)
            logger.LogInformation("User {UserId} confirmed their email", user!.Id);

        ViewData["Confirmed"] = confirmed;
        return View();
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(string? returnUrl)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        if (!user.EmailConfirmed)
        {
            await SendConfirmationAsync(user);
            TempData["Success"] = $"We've sent a new confirmation link to {user.Email}.";
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Dashboard");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && !AccountStatus.IsDisabled(user.LockoutEnd))
        {
            var code = Encode(await userManager.GeneratePasswordResetTokenAsync(user));
            accountEmails.SendPasswordReset(user, Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, code }, Request.Scheme)!);
            logger.LogInformation("Password reset requested for {UserId}", user.Id);
        }

        ViewData["Sent"] = true;
        return View(model);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? code)
    {
        if (string.IsNullOrEmpty(code))
            return RedirectToAction(nameof(ForgotPassword));

        return View(new ResetPasswordWithCodeViewModel { Email = email ?? string.Empty, Code = code });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithCodeViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        var result = user is null
            ? IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "This reset link is invalid or has expired." })
            : await userManager.ResetPasswordAsync(user, Decode(model.Code), model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code.Contains("Password") ? nameof(model.NewPassword) : string.Empty,
                    error.Code == "InvalidToken" ? "This reset link is invalid or has expired. Please request a new one." : error.Description);
            return View(model);
        }

        await userManager.ResetAccessFailedCountAsync(user!);
        logger.LogInformation("User {UserId} reset their password", user!.Id);
        if (user.EmailConfirmed)
            accountEmails.SendPasswordChanged(user);

        TempData["Success"] = "Your password has been reset. You can sign in now.";
        return RedirectToAction(nameof(Login));
    }

    private async Task SendConfirmationAsync(ApplicationUser user)
    {
        var code = Encode(await userManager.GenerateEmailConfirmationTokenAsync(user));
        accountEmails.SendConfirmation(user, user.Email!, Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, code }, Request.Scheme)!);
    }

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string Decode(string code)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private async Task<ManageAccountViewModel> BuildManageAsync(
        ApplicationUser user, ProfileFormViewModel profile, ChangePasswordViewModel? password = null) => new()
    {
        Profile = profile,
        Password = password ?? new ChangePasswordViewModel(),
        Notifications = new NotificationSettingsViewModel
        {
            EmailAppointments = user.EmailAppointments,
            EmailReminders = user.EmailReminders,
            EmailUpdates = user.EmailUpdates,
            RemindWorkouts = user.RemindWorkouts,
            RemindMeals = user.RemindMeals
        },
        EmailConfirmed = user.EmailConfirmed,
        IsClient = await userManager.IsInRoleAsync(user, Roles.Client),
        Role = (await userManager.GetRolesAsync(user)).FirstOrDefault() is { } role ? Roles.DisplayName(role) : "Member",
        TrainerName = user.TrainerId is null ? null : (await userManager.FindByIdAsync(user.TrainerId))?.FullName,
        MemberSince = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };
}
