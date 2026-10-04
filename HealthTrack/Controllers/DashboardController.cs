using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
public class DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(Roles.Admin)) return RedirectToAction(nameof(Admin));
        if (User.IsInRole(Roles.Trainer)) return RedirectToAction(nameof(Trainer));
        if (User.IsInRole(Roles.Client)) return RedirectToAction(nameof(Client));
        return RedirectToAction("AccessDenied", "Account");
    }

    [Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Client() => await GetMemberAsync() is { } model ? View(model) : Challenge();

    [Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Trainer() => await GetMemberAsync() is { } model ? View(model) : Challenge();

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Admin()
    {
        var roleCounts = await (
                from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                group ur by r.Name into g
                select new { Role = g.Key!, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var now = DateTimeOffset.UtcNow;

        var recentUsers = await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(8)
            .Select(u => new RecentUser(
                u.FullName,
                u.Email!,
                (from ur in db.UserRoles
                 join r in db.Roles on ur.RoleId equals r.Id
                 where ur.UserId == u.Id
                 select r.Name).FirstOrDefault() ?? "None",
                u.CreatedAt))
            .ToListAsync();

        return View(new AdminDashboardViewModel
        {
            TotalUsers = await db.Users.CountAsync(),
            Clients = roleCounts.GetValueOrDefault(Roles.Client),
            Trainers = roleCounts.GetValueOrDefault(Roles.Trainer),
            NewThisWeek = await db.Users.CountAsync(u => u.CreatedAt >= weekAgo),
            LockedOut = await db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now),
            RecentUsers = recentUsers
        });
    }

    private async Task<MemberDashboardViewModel?> GetMemberAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return null;

        return new MemberDashboardViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Age = user.DateOfBirth is { } dob ? CalculateAge(dob) : null,
            Gender = user.Gender,
            MemberSince = user.CreatedAt
        };
    }

    private static int CalculateAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dob.Year;
        return dob > today.AddYears(-age) ? age - 1 : age;
    }
}
