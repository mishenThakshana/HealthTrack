using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

// Permanently removes a client and all of their health data (right to erasure).
public class AccountDeletion(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<AccountDeletion> logger)
{
    public async Task<IdentityResult> DeleteClientAsync(ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, Roles.Client))
            return IdentityResult.Failed(new IdentityError { Code = "NotClient", Description = "Only client accounts can be deleted. Disable other accounts instead." });

        var id = user.Id;
        await using var transaction = await db.Database.BeginTransactionAsync();

        var appointmentLinks = await db.Appointments.Where(a => a.ClientId == id)
            .Select(a => "/Appointments/Details/" + a.Id).ToListAsync();
        await db.Notifications.Where(n => n.UserId == id || appointmentLinks.Contains(n.Link!)).ExecuteDeleteAsync();
        await db.ProgressFeedback.Where(f => f.ClientId == id || f.AuthorId == id).ExecuteDeleteAsync();
        await db.Appointments.Where(a => a.ClientId == id).ExecuteDeleteAsync();
        await db.WorkoutExercises.Where(e => e.WorkoutPlan!.ClientId == id).ExecuteDeleteAsync();
        await db.WorkoutPlans.Where(p => p.ClientId == id).ExecuteDeleteAsync();
        await db.DietMeals.Where(m => m.DietPlan!.ClientId == id).ExecuteDeleteAsync();
        await db.DietPlans.Where(p => p.ClientId == id).ExecuteDeleteAsync();
        await db.WorkoutLogs.Where(l => l.ClientId == id).ExecuteDeleteAsync();
        await db.MealLogs.Where(l => l.ClientId == id).ExecuteDeleteAsync();
        await db.HealthMeasurements.Where(m => m.ClientId == id).ExecuteDeleteAsync();

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return result;

        await transaction.CommitAsync();
        logger.LogInformation("Deleted client account {UserId} and all of its data", id);
        return result;
    }
}
