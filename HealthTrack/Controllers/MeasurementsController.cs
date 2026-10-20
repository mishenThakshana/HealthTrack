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

    public async Task<IActionResult> Index(int? edit)
    {
        if (edit is null)
            return View(await BuildPageAsync(new MeasurementFormViewModel { HeightCm = await LastHeightAsync() }));

        var entry = await FindAsync(edit.Value);
        if (entry is null)
            return NotFound();

        return View(await BuildPageAsync(new MeasurementFormViewModel
        {
            Date = entry.Date,
            WeightKg = entry.WeightKg,
            HeightCm = entry.HeightCm,
            BodyFatPercent = entry.BodyFatPercent,
            RestingHeartRate = entry.RestingHeartRate,
            Notes = entry.Notes
        }, entry.Id));
    }

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
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] MeasurementFormViewModel form)
    {
        var entry = await FindAsync(id);
        if (entry is null)
            return NotFound();

        TrackingRules.ValidateDate(ModelState, form.Date, entry.Date);
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildPageAsync(form, id));

        entry.Date = form.Date;
        entry.WeightKg = Math.Round(form.WeightKg!.Value, 1);
        entry.HeightCm = form.HeightCm is { } h ? Math.Round(h, 1) : entry.HeightCm ?? await LastHeightAsync();
        entry.BodyFatPercent = form.BodyFatPercent is { } bf ? Math.Round(bf, 1) : null;
        entry.RestingHeartRate = form.RestingHeartRate;
        entry.Notes = TrackingRules.Clean(form.Notes);
        await db.SaveChangesAsync();

        TempData["Success"] = "Measurement updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await FindAsync(id);
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

    private Task<HealthMeasurement?> FindAsync(int id) => db.HealthMeasurements.FirstOrDefaultAsync(m => m.Id == id && m.ClientId == UserId);

    private async Task<TrackingPageViewModel<MeasurementFormViewModel, HealthMeasurement>> BuildPageAsync(MeasurementFormViewModel form, int? editingId = null) => new()
    {
        Form = form,
        EditingId = editingId,
        Entries = await db.HealthMeasurements.Where(m => m.ClientId == UserId)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Take(TrackingRules.PageSize).ToListAsync()
    };
}
