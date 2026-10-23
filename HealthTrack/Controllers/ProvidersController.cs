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

// Lets clients browse trainers and health professionals, read their reviews and choose one of each to work with.
[Authorize]
public class ProvidersController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    NotificationService notifications,
    ILogger<ProvidersController> logger) : Controller
{
    [Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Index(string? show, string? q, string? focus, string? sort)
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
            return Challenge();

        var showProfessionals = show == "professionals";
        var focusOptions = showProfessionals ? ProfileOptions.Professions : ProfileOptions.Specializations;
        focus = focusOptions.Contains(focus) ? focus : null;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var providers = Available(showProfessionals ? Roles.Professional : Roles.Trainer);
        if (focus is not null)
            providers = providers.Where(u => u.TrainerProfile!.Specialization == focus || u.ProfessionalProfile!.Profession == focus);
        if (q is not null)
            providers = providers.Where(u => u.FullName.Contains(q)
                || u.TrainerProfile!.Specialization.Contains(q)
                || u.TrainerProfile!.Qualifications.Any(x => x.Name.Contains(q))
                || u.ProfessionalProfile!.Profession.Contains(q)
                || u.ProfessionalProfile!.Workplace!.Contains(q));

        var sortByRating = sort == "rating";
        var ordered = sortByRating
            ? providers.OrderByDescending(u => db.AppointmentReviews.Where(r => r.ProviderId == u.Id).Average(r => (double?)r.Rating) ?? 0)
                .ThenByDescending(u => db.AppointmentReviews.Count(r => r.ProviderId == u.Id)).ThenBy(u => u.FullName)
            : providers.OrderBy(u => u.FullName);
        var mine = await Cards(db, db.Users.Where(u => u.Id == me.TrainerId || u.Id == me.ProfessionalId)).ToListAsync();

        return View(new ProvidersPageViewModel
        {
            ShowProfessionals = showProfessionals,
            Search = q,
            Focus = focus,
            SortByRating = sortByRating,
            FocusOptions = focusOptions,
            Providers = await Cards(db, ordered).ToListAsync(),
            MyTrainer = mine.FirstOrDefault(p => p.Id == me.TrainerId),
            MyProfessional = mine.FirstOrDefault(p => p.Id == me.ProfessionalId)
        });
    }

    // A trainer's or professional's profile with their reviews. Open to clients, admins, and the person themselves.
    public async Task<IActionResult> Details(string id)
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
            return Challenge();

        var isClient = User.IsInRole(Roles.Client);
        var isAdmin = User.IsInRole(Roles.Admin);
        if (!isClient && !isAdmin && me.Id != id)
            return NotFound();

        var isTrainer = await db.UsersInRole(Roles.Trainer).AnyAsync(u => u.Id == id);
        var providers = isTrainer ? db.UsersInRole(Roles.Trainer) : db.UsersInRole(Roles.Professional);
        // Clients can't open disabled accounts; admins and the person themselves can.
        var provider = await Cards(db, (isClient ? Available(isTrainer ? Roles.Trainer : Roles.Professional) : providers).Where(u => u.Id == id))
            .FirstOrDefaultAsync();
        if (provider is null)
            return NotFound();

        var reviews = db.AppointmentReviews.Where(r => r.ProviderId == id);
        var starCounts = await reviews.GroupBy(r => r.Rating).Select(g => new { Stars = g.Key, Count = g.Count() }).ToListAsync();
        var currentChoiceId = isTrainer ? me.TrainerId : me.ProfessionalId;

        return View(new ProviderProfileViewModel
        {
            Provider = provider,
            IsTrainer = isTrainer,
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

        var isTrainer = await Available(Roles.Trainer).AnyAsync(u => u.Id == id);
        var name = await Available(isTrainer ? Roles.Trainer : Roles.Professional)
            .Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefaultAsync();
        if (name is null)
            return NotFound();

        var noun = isTrainer ? "trainer" : "health professional";
        var previousId = isTrainer ? me.TrainerId : me.ProfessionalId;
        if (previousId != id)
        {
            if (isTrainer)
                me.TrainerId = id;
            else
                me.ProfessionalId = id;
            await db.SaveChangesAsync();
            logger.LogInformation("Client {ClientId} chose {ProviderId} as their {Role}", me.Id, id, noun);

            if (isTrainer)
                await notifications.NotifyAsync(id, NotificationType.TrainerAssigned, "New client",
                    $"{me.FullName} chose you as their trainer.", $"/Clients/Details/{me.Id}");
            else
                await notifications.NotifyAsync(id, NotificationType.ProfessionalChosen, "New client",
                    $"{me.FullName} chose you as their health professional. You can now see their progress.", $"/Progress/Index/{me.Id}");

            if (previousId is not null)
                await notifications.NotifyAsync(previousId, NotificationType.ClientLeft, $"{me.FullName} has moved on",
                    $"{me.FullName} chose a different {noun}, so you can no longer see their progress.");
        }

        TempData["Success"] = $"{name} is now your {noun}.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index), new { show = isTrainer ? null : "professionals" });
    }

    [HttpPost, Authorize(Roles = Roles.Client), ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(string role)
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
            return Challenge();

        var isTrainer = role == Roles.Trainer;
        var previousId = isTrainer ? me.TrainerId : me.ProfessionalId;
        var noun = isTrainer ? "trainer" : "health professional";
        if (previousId is not null)
        {
            if (isTrainer)
                me.TrainerId = null;
            else
                me.ProfessionalId = null;
            await db.SaveChangesAsync();
            logger.LogInformation("Client {ClientId} removed {ProviderId} as their {Role}", me.Id, previousId, noun);

            await notifications.NotifyAsync(previousId, NotificationType.ClientLeft, $"{me.FullName} has moved on",
                $"{me.FullName} removed you as their {noun}, so you can no longer see their progress.");
            TempData["Success"] = $"You no longer have a {noun}.";
        }

        return RedirectToAction(nameof(Index), new { show = isTrainer ? null : "professionals" });
    }

    // Trainers or professionals clients can choose: disabled accounts are left out.
    private IQueryable<ApplicationUser> Available(string role)
    {
        var disabledFrom = DateTimeOffset.UtcNow.AddYears(50);
        return db.UsersInRole(role).Where(u => u.LockoutEnd == null || u.LockoutEnd <= disabledFrom);
    }

    public static IQueryable<ProviderCard> Cards(ApplicationDbContext db, IQueryable<ApplicationUser> users) =>
        users.Select(u => new ProviderCard(
            u.Id, u.FullName, u.PhotoUpdatedAt,
            u.TrainerProfile != null ? u.TrainerProfile.Specialization : u.ProfessionalProfile != null ? u.ProfessionalProfile.Profession : null,
            u.TrainerProfile != null ? u.TrainerProfile.YearsOfExperience : u.ProfessionalProfile != null ? u.ProfessionalProfile.YearsOfExperience : null,
            u.TrainerProfile != null ? u.TrainerProfile.Qualifications.OrderBy(x => x.Id).Select(x => x.Name).ToList() : new List<string>(),
            u.ProfessionalProfile != null ? u.ProfessionalProfile.Workplace : null,
            db.Users.Count(c => c.TrainerId == u.Id || c.ProfessionalId == u.Id),
            db.AppointmentReviews.Where(r => r.ProviderId == u.Id).Average(r => (double?)r.Rating),
            db.AppointmentReviews.Count(r => r.ProviderId == u.Id)));
}
