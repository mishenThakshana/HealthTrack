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

// Lets clients browse trainers, read their reviews and choose one to work with.
[Authorize]
public class ProvidersController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    NotificationService notifications,
    ILogger<ProvidersController> logger) : Controller
{
    [Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Index(string? q, string? focus, string? sort)
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
            return Challenge();

        focus = ProfileOptions.Specializations.Contains(focus) ? focus : null;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var providers = Available();
        if (focus is not null)
            providers = providers.Where(u => u.TrainerProfile!.Specialization == focus);
        if (q is not null)
            providers = providers.Where(u => u.FullName.Contains(q)
                || u.TrainerProfile!.Specialization.Contains(q)
                || u.TrainerProfile!.Qualifications.Any(x => x.Name.Contains(q)));

        var sortByRating = sort == "rating";
        var ordered = sortByRating
            ? providers.OrderByDescending(u => db.AppointmentReviews.Where(r => r.ProviderId == u.Id).Average(r => (double?)r.Rating) ?? 0)
                .ThenByDescending(u => db.AppointmentReviews.Count(r => r.ProviderId == u.Id)).ThenBy(u => u.FullName)
            : providers.OrderBy(u => u.FullName);

        return View(new ProvidersPageViewModel
        {
            Search = q,
            Focus = focus,
            SortByRating = sortByRating,
            FocusOptions = ProfileOptions.Specializations,
            Providers = await Cards(db, ordered).ToListAsync(),
            MyTrainer = await Cards(db, db.Users.Where(u => u.Id == me.TrainerId)).FirstOrDefaultAsync()
        });
    }

    // A trainer's profile with their reviews. Open to clients, admins, and the person themselves.
    public async Task<IActionResult> Details(string id)
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
            return Challenge();

        var isClient = User.IsInRole(Roles.Client);
        var isAdmin = User.IsInRole(Roles.Admin);
        if (!isClient && !isAdmin && me.Id != id)
            return NotFound();

        // Clients can't open disabled accounts; admins and the trainer themselves can.
        var provider = await Cards(db, (isClient ? Available() : db.UsersInRole(Roles.Trainer)).Where(u => u.Id == id))
            .FirstOrDefaultAsync();
        if (provider is null)
            return NotFound();

        var reviews = db.AppointmentReviews.Where(r => r.ProviderId == id);
        var starCounts = await reviews.GroupBy(r => r.Rating).Select(g => new { Stars = g.Key, Count = g.Count() }).ToListAsync();
        var currentChoiceId = me.TrainerId;

        return View(new ProviderProfileViewModel
        {
            Provider = provider,
            StarCounts = Enumerable.Range(1, 5).Select(stars => starCounts.FirstOrDefault(s => s.Stars == stars)?.Count ?? 0).ToList(),
            Reviews = await reviews.OrderByDescending(r => r.CreatedAt).Take(50)
                .Select(r => new ReviewItem(r.Id, r.AppointmentId, r.Client!.FullName, r.Rating, r.Comment, r.CreatedAt))
                .ToListAsync(),
            IsClient = isClient,
            IsAdmin = isAdmin,
            IsSelf = me.Id == id,
            IsMine = isClient && currentChoiceId == id,
            CurrentChoiceName = isClient && currentChoiceId is not null && currentChoiceId != id
                ? await db.Users.Where(u => u.Id == currentChoiceId).Select(u => u.FullName).FirstOrDefaultAsync()
                : null
        });
    }

    [HttpPost, Authorize(Roles = Roles.Client), ValidateAntiForgeryToken]
    public async Task<IActionResult> Choose(string id, string? returnUrl)
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
            return Challenge();

        var name = await Available().Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefaultAsync();
        if (name is null)
            return NotFound();

        var previousId = me.TrainerId;
        if (previousId != id)
        {
            me.TrainerId = id;
            await db.SaveChangesAsync();
            logger.LogInformation("Client {ClientId} chose {TrainerId} as their trainer", me.Id, id);

            await notifications.NotifyAsync(id, NotificationType.TrainerAssigned, "New client",
                $"{me.FullName} chose you as their trainer.", $"/Clients/Details/{me.Id}");
            if (previousId is not null)
                await notifications.NotifyAsync(previousId, NotificationType.ClientLeft, $"{me.FullName} has moved on",
                    $"{me.FullName} chose a different trainer, so you can no longer see their progress.");
        }

        TempData["Success"] = $"{name} is now your trainer.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = Roles.Client), ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave()
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
            return Challenge();

        var previousId = me.TrainerId;
        if (previousId is not null)
        {
            me.TrainerId = null;
            await db.SaveChangesAsync();
            logger.LogInformation("Client {ClientId} removed {TrainerId} as their trainer", me.Id, previousId);

            await notifications.NotifyAsync(previousId, NotificationType.ClientLeft, $"{me.FullName} has moved on",
                $"{me.FullName} removed you as their trainer, so you can no longer see their progress.");
            TempData["Success"] = "You no longer have a trainer.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Trainers clients can choose: disabled accounts are left out.
    private IQueryable<ApplicationUser> Available()
    {
        var disabledFrom = DateTimeOffset.UtcNow.AddYears(50);
        return db.UsersInRole(Roles.Trainer).Where(u => u.LockoutEnd == null || u.LockoutEnd <= disabledFrom);
    }

    public static IQueryable<ProviderCard> Cards(ApplicationDbContext db, IQueryable<ApplicationUser> users) =>
        users.Select(u => new ProviderCard(
            u.Id, u.FullName, u.PhotoUpdatedAt,
            u.TrainerProfile != null ? u.TrainerProfile.Specialization : null,
            u.TrainerProfile != null ? u.TrainerProfile.YearsOfExperience : null,
            u.TrainerProfile != null ? u.TrainerProfile.Qualifications.OrderBy(x => x.Id).Select(x => x.Name).ToList() : new List<string>(),
            db.Users.Count(c => c.TrainerId == u.Id),
            db.AppointmentReviews.Where(r => r.ProviderId == u.Id).Average(r => (double?)r.Rating),
            db.AppointmentReviews.Count(r => r.ProviderId == u.Id)));
}
