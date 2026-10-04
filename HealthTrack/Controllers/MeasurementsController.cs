using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Client)]
public class MeasurementsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private string UserId => userManager.GetUserId(User)!;

    public async Task<IActionResult> Index() =>
        View(await BuildPageAsync(new MeasurementFormViewModel { HeightCm = await LastHeightAsync() }));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] MeasurementFormViewModel form)
    {
        TrackingRules.ValidateDate(ModelState, form.Date);
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildPageAsync(form));

        db.HealthMeasurements.Add(new HealthMeasurement
        {
            ClientId = UserId,
            Date = form.Date,
            WeightKg = Math.Round(form.WeightKg!.Value, 1),
            HeightCm = form.HeightCm is { } h ? Math.Round(h, 1) : await LastHeightAsync(),
            BodyFatPercent = form.BodyFatPercent is { } bf ? Math.Round(bf, 1) : null,
            RestingHeartRate = form.RestingHeartRate,
            Notes = TrackingRules.Clean(form.Notes)
        });
        await db.SaveChangesAsync();

        TempData["Success"] = "Measurement saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await db.HealthMeasurements.FirstOrDefaultAsync(m => m.Id == id && m.ClientId == UserId);
        if (entry is null)
            return NotFound();

        db.HealthMeasurements.Remove(entry);
        await db.SaveChangesAsync();

        TempData["Success"] = "Measurement deleted.";
        return RedirectToAction(nameof(Index));
    }

    private Task<decimal?> LastHeightAsync() =>
        db.HealthMeasurements
            .Where(m => m.ClientId == UserId && m.HeightCm != null)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Select(m => m.HeightCm).FirstOrDefaultAsync();

    private async Task<TrackingPageViewModel<MeasurementFormViewModel, HealthMeasurement>> BuildPageAsync(MeasurementFormViewModel form) => new()
    {
        Form = form,
        Entries = await db.HealthMeasurements.Where(m => m.ClientId == UserId)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Take(TrackingRules.PageSize).ToListAsync()
    };
}
