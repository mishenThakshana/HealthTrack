using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    AccountEmails accountEmails,
    ILogger<UsersController> logger) : Controller
{
    private string UserId => userManager.GetUserId(User)!;

    public async Task<IActionResult> Index(string? q, string? role)
    {
        q = TrackingRules.Clean(q);
        role = Roles.All.Contains(role) ? role : null;

        var users = role is null ? db.Users : db.UsersInRole(role);
        if (q is not null)
            users = users.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q));

        var rows = await users
            .OrderBy(u => u.FullName)
            .Take(200)
            .Select(u => new UserRow(
                u.Id, u.FullName, u.Email!,
                (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == u.Id select r.Name)
                    .FirstOrDefault() ?? "None",
                u.Trainer != null ? u.Trainer.FullName : null,
                u.CreatedAt, u.LastLoginAt, u.LockoutEnd))
            .ToListAsync();

        return View(new UsersIndexViewModel { Query = q, Role = role, Users = rows, Total = await db.Users.CountAsync() });
    }

    public IActionResult Create() => View(new UserFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel form)
    {
        ValidateProfile(form);
        if (string.IsNullOrEmpty(form.Password))
            ModelState.AddModelError(nameof(form.Password), "Please set a password.");
        if (!ModelState.IsValid)
            return View(form);

        var user = new ApplicationUser
        {
            UserName = form.Email.Trim(),
            Email = form.Email.Trim(),
            EmailConfirmed = true,
            FullName = form.FullName.Trim(),
            DateOfBirth = form.DateOfBirth,
            Gender = form.Gender
        };

        var result = await userManager.CreateAsync(user, form.Password!);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(form);
        }

        await userManager.AddToRoleAsync(user, form.Role);
        logger.LogInformation("Admin {AdminId} created {Role} account {UserId}", UserId, form.Role, user.Id);

        TempData["Success"] = $"Created {Roles.DisplayName(form.Role).ToLowerInvariant()} account for {user.FullName}.";
        return RedirectToAction(nameof(Edit), new { id = user.Id });
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        return View(await BuildEditPageAsync(user, new UserFormViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = await RoleOfAsync(user),
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender
        }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind(Prefix = "Form")] UserFormViewModel form)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        ModelState.Remove("Form.Password");
        ModelState.Remove("Form.ConfirmPassword");
        ValidateProfile(form, "Form.");

        var currentRole = await RoleOfAsync(user);
        if (form.Role != currentRole && ModelState.IsValid)
        {
            if (user.Id == UserId)
                ModelState.AddModelError("Form.Role", "You can't change your own role.");
            else if (await RoleChangeBlockerAsync(user, currentRole) is { } blocker)
                ModelState.AddModelError("Form.Role", blocker);
        }

        if (!ModelState.IsValid)
            return View(await BuildEditPageAsync(user, form));

        user.FullName = form.FullName.Trim();
        user.Email = form.Email.Trim();
        user.UserName = form.Email.Trim();
        user.DateOfBirth = form.DateOfBirth;
        user.Gender = form.Gender;
        if (form.Role != Roles.Client)
            user.TrainerId = null;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddErrors(result, "Form.");
            return View(await BuildEditPageAsync(user, form));
        }

        if (form.Role != currentRole)
        {
            if (Roles.All.Contains(currentRole))
                await userManager.RemoveFromRoleAsync(user, currentRole);
            await userManager.AddToRoleAsync(user, form.Role);
            await userManager.UpdateSecurityStampAsync(user);
            logger.LogInformation("Admin {AdminId} changed {UserId} from {OldRole} to {NewRole}", UserId, user.Id, currentRole, form.Role);
        }

        TempData["Success"] = $"Saved changes to {user.FullName}.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, [Bind(Prefix = "Reset")] ResetPasswordViewModel reset)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (ModelState.IsValid)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, reset.NewPassword);
            if (result.Succeeded)
            {
                logger.LogInformation("Admin {AdminId} reset the password for {UserId}", UserId, user.Id);
                if (user.EmailConfirmed)
                    accountEmails.SendPasswordChanged(user);
                TempData["Success"] = $"Password reset for {user.FullName}. Share the new password with them securely.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            AddErrors(result, "Reset.");
        }

        return View(nameof(Edit), await BuildEditPageAsync(user, new UserFormViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = await RoleOfAsync(user),
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender
        }, reset));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnabled(string id, bool enabled)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (user.Id == UserId)
        {
            TempData["Error"] = "You can't disable your own account.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (enabled)
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, AccountStatus.DisabledUntil);
        }
        await userManager.UpdateSecurityStampAsync(user);
        logger.LogInformation("Admin {AdminId} {Action} account {UserId}", UserId, enabled ? "enabled" : "disabled", user.Id);

        TempData["Success"] = enabled ? $"{user.FullName} can sign in again." : $"{user.FullName}'s account is disabled.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private void ValidateProfile(UserFormViewModel form, string prefix = "")
    {
        if (!Roles.All.Contains(form.Role))
            ModelState.AddModelError(prefix + nameof(form.Role), "Please choose a valid role.");
        if (form.Gender is not null && !AccountController.Genders.Contains(form.Gender))
            ModelState.AddModelError(prefix + nameof(form.Gender), "Please choose a valid option.");
        if (form.DateOfBirth is { } dob && (dob > Dates.Today || dob.Year < 1900))
            ModelState.AddModelError(prefix + nameof(form.DateOfBirth), "Please enter a valid date of birth.");
    }

    private void AddErrors(IdentityResult result, string prefix = "")
    {
        foreach (var error in result.Errors.Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            var key = error.Code.Contains("Password") ? prefix + (prefix == "Reset." ? "NewPassword" : "Password")
                : error.Code.Contains("Email") ? prefix + "Email" : string.Empty;
            ModelState.AddModelError(key, error.Description);
        }
    }

    private async Task<string> RoleOfAsync(ApplicationUser user) =>
        (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "None";

    private async Task<string?> RoleChangeBlockerAsync(ApplicationUser user, string currentRole)
    {
        if (currentRole == Roles.Trainer && await db.Users.CountAsync(u => u.TrainerId == user.Id) is > 0 and var clients)
            return $"{user.FullName} still has {clients} assigned client{(clients == 1 ? "" : "s")}. Reassign them first.";

        var now = DateTime.Now;
        if (await db.Appointments.AnyAsync(a => (a.TrainerId == user.Id || a.ClientId == user.Id) && a.StartsAt > now
                && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved)))
            return $"{user.FullName} has upcoming appointments. They need to be cancelled or completed first.";

        return null;
    }

    private async Task<UserEditPageViewModel> BuildEditPageAsync(ApplicationUser user, UserFormViewModel form, ResetPasswordViewModel? reset = null) => new()
    {
        Id = user.Id,
        Form = form,
        Reset = reset ?? new ResetPasswordViewModel(),
        IsSelf = user.Id == UserId,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
        LockoutEnd = user.LockoutEnd,
        TrainerName = await db.Users.Where(u => u.Id == user.TrainerId).Select(u => u.FullName).FirstOrDefaultAsync()
    };
}
