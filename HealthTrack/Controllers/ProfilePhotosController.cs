using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
public class ProfilePhotosController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ILogger<ProfilePhotosController> logger) : Controller
{
    private string UserId => userManager.GetUserId(User)!;

    // The ?v= query string changes with every upload, so browsers can keep a copy for a long time.
    [HttpGet]
    public async Task<IActionResult> Show(string id)
    {
        if (!await CanSeeAsync(id))
            return NotFound();

        var photo = await db.ProfilePhotos.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id);
        if (photo is null)
            return NotFound();

        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return File(photo.Data, photo.ContentType);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(ProfilePhotoRules.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? photo)
    {
        if (ProfilePhotoRules.Validate(photo) is { } error)
            return Back(error);

        using var buffer = new MemoryStream();
        await photo!.CopyToAsync(buffer);
        var data = buffer.ToArray();
        var contentType = ProfilePhotoRules.DetectContentType(data);
        if (contentType is null)
            return Back("Please upload a JPG, PNG or WebP picture.");

        var existing = await db.ProfilePhotos.FindAsync(UserId);
        if (existing is null)
            db.ProfilePhotos.Add(new ProfilePhoto { UserId = UserId, Data = data, ContentType = contentType });
        else
            (existing.Data, existing.ContentType) = (data, contentType);

        await db.SaveChangesAsync();
        await db.Users.Where(u => u.Id == UserId).ExecuteUpdateAsync(u => u.SetProperty(x => x.PhotoUpdatedAt, DateTime.UtcNow));
        logger.LogInformation("User {UserId} uploaded a profile picture ({Bytes} bytes)", UserId, data.Length);

        TempData["Success"] = "Your profile picture has been updated.";
        return RedirectToAction("Manage", "Account");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove()
    {
        await db.ProfilePhotos.Where(p => p.UserId == UserId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == UserId).ExecuteUpdateAsync(u => u.SetProperty(x => x.PhotoUpdatedAt, (DateTime?)null));

        TempData["Success"] = "Your profile picture has been removed.";
        return RedirectToAction("Manage", "Account");
    }

    // Trainers' and professionals' pictures are visible to everyone signed in, so clients can browse them.
    // A client's picture is only shown to themselves, admins, and the trainers or professionals who work with them.
    private async Task<bool> CanSeeAsync(string id)
    {
        if (id == UserId || User.IsInRole(Roles.Admin))
            return true;

        if (await db.UsersInRole(Roles.Trainer).AnyAsync(u => u.Id == id) || await db.UsersInRole(Roles.Professional).AnyAsync(u => u.Id == id))
            return true;

        return await db.Users.AnyAsync(u => u.Id == id && (u.TrainerId == UserId || u.ProfessionalId == UserId))
            || await db.Appointments.AnyAsync(a => a.ClientId == id && a.TrainerId == UserId);
    }

    private RedirectToActionResult Back(string error)
    {
        TempData["Error"] = error;
        return RedirectToAction("Manage", "Account");
    }
}
