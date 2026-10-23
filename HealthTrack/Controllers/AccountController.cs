using System.Text;
using System.Text.Json;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AccountEmails accountEmails,
    AccountDeletion accountDeletion,
    ApplicationDbContext db,
    ILogger<AccountController> logger) : Controller
{
    public static readonly string[] RegisterableRoles = [Roles.Client, Roles.Trainer, Roles.Professional];
    public static readonly string[] Genders = ["Male", "Female"];

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Dashboard");

        return View(new RegisterViewModel());
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!RegisterableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Please choose a valid account type.");

        if (model.Gender is not null && !Genders.Contains(model.Gender))
            ModelState.AddModelError(nameof(model.Gender), "Please choose a valid option.");

        var isClient = model.Role == Roles.Client;
        if (!isClient)
            ModelState.Remove(nameof(model.DateOfBirth));
        else if (model.DateOfBirth is { } dob && (dob > DateOnly.FromDateTime(DateTime.Today) || dob.Year < 1900))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Please enter a valid date of birth.");

        // Drops the sections for other roles. Clients have no role details section.
        RoleProfiles.Validate(ModelState, model.Role, new(), model.Trainer, model.Professional);

        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            DateOfBirth = isClient ? model.DateOfBirth : null,
            Gender = model.Gender,
            LastLoginAt = DateTime.UtcNow,
            PrivacyAcceptedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors.Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateUserName)))
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await userManager.AddToRoleAsync(user, model.Role);
        if (!isClient)
        {
            await RoleProfiles.ApplyAsync(db, user.Id, model.Role, new(), model.Trainer, model.Professional);
            await db.SaveChangesAsync();
        }
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
    [EnableRateLimiting(RateLimits.Auth)]
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
            logger.LogWarning("Sign-in blocked by lockout for {UserId}",
                (await userManager.FindByEmailAsync(model.Email.Trim()))?.Id ?? "unknown user");
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

        return View(await BuildManageAsync(user, ToProfileForm(user)));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileFormViewModel form)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var isAdmin = User.IsInRole(Roles.Admin);
        if (isAdmin)
            ModelState.Remove("Profile.Gender");
        else if (form.Gender is not null && !Genders.Contains(form.Gender))
            ModelState.AddModelError("Profile.Gender", "Please choose a valid option.");
        if (form.DateOfBirth is { } dob && (dob > DateOnly.FromDateTime(DateTime.Today) || dob.Year < 1900))
            ModelState.AddModelError("Profile.DateOfBirth", "Please enter a valid date of birth.");
        else if (form.DateOfBirth is null && User.IsInRole(Roles.Client))
            ModelState.AddModelError("Profile.DateOfBirth", "Please enter your date of birth.");

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
        user.Gender = isAdmin ? null : form.Gender;
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
    [EnableRateLimiting(RateLimits.Auth)]
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

        return View(nameof(Manage), await BuildManageAsync(user, ToProfileForm(user), form));
    }

    [HttpPost, Authorize(Roles = Roles.Client + "," + Roles.Providers), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDetails(
        [Bind(Prefix = RoleProfiles.ClientPrefix)] ClientProfileFormViewModel client,
        [Bind(Prefix = RoleProfiles.TrainerPrefix)] TrainerProfileFormViewModel trainer,
        [Bind(Prefix = RoleProfiles.ProfessionalPrefix)] ProfessionalProfileFormViewModel professional)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        ModelState.Clear();
        TryValidateModel(client, RoleProfiles.ClientPrefix);
        TryValidateModel(trainer, RoleProfiles.TrainerPrefix);
        TryValidateModel(professional, RoleProfiles.ProfessionalPrefix);
        RoleProfiles.Validate(ModelState, role, client, trainer, professional);

        if (!ModelState.IsValid)
            return View(nameof(Manage), await BuildManageAsync(user, ToProfileForm(user), details: (client, trainer, professional)));

        await RoleProfiles.ApplyAsync(db, user.Id, role, client, trainer, professional);
        await db.SaveChangesAsync();

        TempData["Success"] = "Your details have been saved.";
        return RedirectToAction(nameof(Manage), null, "details");
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

    [HttpGet, Authorize]
    public async Task<IActionResult> DownloadData()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var id = user.Id;
        var export = new
        {
            ExportedAt = DateTime.UtcNow,
            Profile = new
            {
                user.FullName, user.Email, user.EmailConfirmed, user.DateOfBirth, user.Gender,
                Role = (await userManager.GetRolesAsync(user)).FirstOrDefault(),
                Trainer = await db.Users.Where(u => u.Id == user.TrainerId).Select(u => u.FullName).FirstOrDefaultAsync(),
                HealthProfessional = await db.Users.Where(u => u.Id == user.ProfessionalId).Select(u => u.FullName).FirstOrDefaultAsync(),
                HasProfilePicture = user.PhotoUpdatedAt is not null,
                user.CreatedAt, user.LastLoginAt, user.PrivacyAcceptedAt,
                NotificationSettings = new { user.EmailAppointments, user.EmailReminders, user.EmailUpdates, user.RemindWorkouts, user.RemindMeals }
            },
            HealthDetails = await db.ClientProfiles.Where(p => p.UserId == id)
                .Select(p => new { p.FitnessGoal, p.ActivityLevel, p.HealthConditions, p.UpdatedAt }).FirstOrDefaultAsync(),
            CoachingDetails = await db.TrainerProfiles.Where(p => p.UserId == id)
                .Select(p => new { p.Specialization, p.YearsOfExperience, Qualifications = p.Qualifications.OrderBy(q => q.Id).Select(q => q.Name).ToList(), p.UpdatedAt })
                .FirstOrDefaultAsync(),
            PracticeDetails = await db.ProfessionalProfiles.Where(p => p.UserId == id)
                .Select(p => new { p.Profession, p.RegistrationNumber, p.Workplace, p.YearsOfExperience, p.UpdatedAt }).FirstOrDefaultAsync(),
            Measurements = await db.HealthMeasurements.Where(m => m.ClientId == id).OrderBy(m => m.Date)
                .Select(m => new { m.Date, m.WeightKg, m.HeightCm, m.BodyFatPercent, m.RestingHeartRate, m.Notes, m.CreatedAt }).ToListAsync(),
            Workouts = await db.WorkoutLogs.Where(l => l.ClientId == id).OrderBy(l => l.Date)
                .Select(l => new { l.Date, l.Activity, l.DurationMinutes, l.CaloriesBurned, l.Notes, l.CreatedAt }).ToListAsync(),
            Meals = await db.MealLogs.Where(l => l.ClientId == id).OrderBy(l => l.Date)
                .Select(l => new { l.Date, MealType = l.MealType.ToString(), l.Description, l.Calories, l.CreatedAt }).ToListAsync(),
            WorkoutPlans = await db.WorkoutPlans.Where(p => p.ClientId == id || p.TrainerId == id)
                .Select(p => new
                {
                    p.Title, p.Description, p.StartDate, p.EndDate, p.IsActive, Client = p.Client!.FullName, Trainer = p.Trainer!.FullName,
                    Exercises = p.Exercises.Select(e => new { Day = e.Day.ToString(), e.Name, e.Sets, e.Reps, e.DurationMinutes, e.Notes })
                }).ToListAsync(),
            DietPlans = await db.DietPlans.Where(p => p.ClientId == id || p.TrainerId == id)
                .Select(p => new
                {
                    p.Title, p.Description, p.DailyCalorieTarget, p.StartDate, p.EndDate, p.IsActive, Client = p.Client!.FullName, Trainer = p.Trainer!.FullName,
                    Meals = p.Meals.Select(m => new { MealType = m.MealType.ToString(), m.Description, m.Calories })
                }).ToListAsync(),
            Appointments = await db.Appointments.Where(a => a.ClientId == id || a.TrainerId == id).OrderBy(a => a.StartsAt)
                .Select(a => new
                {
                    a.StartsAt, a.EndsAt, Mode = a.Mode.ToString(), Status = a.Status.ToString(), a.Location, a.Reason, a.TrainerNote,
                    Client = a.Client!.FullName, Provider = a.Trainer!.FullName, a.CreatedAt
                }).ToListAsync(),
            Feedback = await db.ProgressFeedback.Where(f => f.ClientId == id || f.AuthorId == id).OrderBy(f => f.CreatedAt)
                .Select(f => new { Client = f.Client!.FullName, Author = f.Author!.FullName, f.Message, f.CreatedAt }).ToListAsync(),
            ReviewsWritten = await db.AppointmentReviews.Where(r => r.ClientId == id).OrderBy(r => r.CreatedAt)
                .Select(r => new { Provider = r.Provider!.FullName, AppointmentAt = r.Appointment!.StartsAt, r.Rating, r.Comment, r.CreatedAt, r.UpdatedAt })
                .ToListAsync(),
            ReviewsReceived = await db.AppointmentReviews.Where(r => r.ProviderId == id).OrderBy(r => r.CreatedAt)
                .Select(r => new { Client = r.Client!.FullName, AppointmentAt = r.Appointment!.StartsAt, r.Rating, r.Comment, r.CreatedAt })
                .ToListAsync(),
            Notifications = await db.Notifications.Where(n => n.UserId == id).OrderBy(n => n.CreatedAt)
                .Select(n => new { Type = n.Type.ToString(), n.Title, n.Message, n.CreatedAt, n.ReadAt }).ToListAsync()
        };

        logger.LogInformation("User {UserId} downloaded their data", id);
        var json = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
        return File(json, "application/json", $"healthtrack-my-data-{DateTime.Today:yyyy-MM-dd}.json");
    }

    [HttpPost, Authorize(Roles = Roles.Client), ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> DeleteAccount(string? password, bool confirm)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        if (!confirm || string.IsNullOrEmpty(password) || !await userManager.CheckPasswordAsync(user, password))
        {
            TempData["Error"] = "Your account wasn't deleted. Tick the box and enter your current password to confirm.";
            return RedirectToAction(nameof(Manage), null, "delete");
        }

        var email = user.Email;
        var name = user.FullName;
        var confirmed = user.EmailConfirmed;
        var result = await accountDeletion.DeleteClientAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Manage));
        }

        await signInManager.SignOutAsync();
        if (confirmed && email is not null)
            accountEmails.SendAccountDeleted(name, email);

        TempData["Success"] = "Your account and all of your health data have been permanently deleted.";
        return RedirectToAction("Index", "Home");
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
    [EnableRateLimiting(RateLimits.Auth)]
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
    [EnableRateLimiting(RateLimits.Auth)]
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
    [EnableRateLimiting(RateLimits.Auth)]
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

    private static ProfileFormViewModel ToProfileForm(ApplicationUser user) => new()
    {
        FullName = user.FullName,
        Email = user.Email ?? string.Empty,
        DateOfBirth = user.DateOfBirth,
        Gender = user.Gender
    };

    private async Task<ManageAccountViewModel> BuildManageAsync(
        ApplicationUser user, ProfileFormViewModel profile, ChangePasswordViewModel? password = null,
        (ClientProfileFormViewModel Client, TrainerProfileFormViewModel Trainer, ProfessionalProfileFormViewModel Professional)? details = null)
    {
        var (client, trainer, professional) = details ?? await RoleProfiles.LoadFormsAsync(db, user.Id);
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault();
        return new ManageAccountViewModel
        {
            Profile = profile,
            Password = password ?? new ChangePasswordViewModel(),
            Client = client,
            Trainer = trainer,
            Professional = professional,
            RoleKey = role ?? string.Empty,
            Notifications = new NotificationSettingsViewModel
            {
                EmailAppointments = user.EmailAppointments,
                EmailReminders = user.EmailReminders,
                EmailUpdates = user.EmailUpdates,
                RemindWorkouts = user.RemindWorkouts,
                RemindMeals = user.RemindMeals
            },
            EmailConfirmed = user.EmailConfirmed,
            IsClient = role == Roles.Client,
            Role = role is not null ? Roles.DisplayName(role) : "Member",
            TrainerName = user.TrainerId is null ? null : (await userManager.FindByIdAsync(user.TrainerId))?.FullName,
            ProfessionalName = user.ProfessionalId is null ? null : (await userManager.FindByIdAsync(user.ProfessionalId))?.FullName,
            Avatar = Avatar.Of(user, "ht-avatar-xl"),
            MemberSince = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}
