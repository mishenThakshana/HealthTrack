using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
public class NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private string UserId => userManager.GetUserId(User)!;

    public async Task<IActionResult> Index() =>
        View(await db.Notifications.Where(n => n.UserId == UserId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(TrackingRules.PageSize).ToListAsync());

    public async Task<IActionResult> Open(int id)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (notification is null)
            return NotFound();

        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Url.IsLocalUrl(notification.Link) ? LocalRedirect(notification.Link) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var now = DateTime.UtcNow;
        await db.Notifications.Where(n => n.UserId == UserId && n.ReadAt == null)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.ReadAt, now));

        TempData["Success"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearRead()
    {
        var removed = await db.Notifications.Where(n => n.UserId == UserId && n.ReadAt != null).ExecuteDeleteAsync();

        TempData["Success"] = removed == 0 ? "There were no read notifications to clear." : $"Cleared {removed} read notification{(removed == 1 ? "" : "s")}.";
        return RedirectToAction(nameof(Index));
    }
}
