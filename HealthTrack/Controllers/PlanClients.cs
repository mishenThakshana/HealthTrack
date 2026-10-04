using HealthTrack.Data;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

internal static class PlanClients
{
    public static async Task<List<SelectListItem>> SelectListAsync(
        ApplicationDbContext db, string trainerId, string? selected, string? alsoInclude = null) =>
        await db.Users
            .Where(u => u.TrainerId == trainerId || u.Id == alsoInclude)
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem(u.FullName, u.Id, u.Id == selected))
            .ToListAsync();

    public static async Task ValidateAsync(
        ApplicationDbContext db, ModelStateDictionary modelState, PlanFormViewModel model, string trainerId, string? currentClientId)
    {
        if (model.EndDate is { } end && end < model.StartDate)
            modelState.AddModelError(nameof(model.EndDate), "End date can't be before the start date.");

        if (string.IsNullOrEmpty(model.ClientId) || model.ClientId == currentClientId)
            return;

        if (!await db.Users.AnyAsync(u => u.Id == model.ClientId && u.TrainerId == trainerId))
            modelState.AddModelError(nameof(model.ClientId), "You can only create plans for your own clients.");
    }
}
