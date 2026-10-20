using HealthTrack.Data;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HealthTrack.ViewComponents;

public class NotificationBellViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var mine = db.Notifications.Where(n => n.UserId == userId);

        return View(new NotificationBellViewModel
        {
            Unread = await mine.CountAsync(n => n.ReadAt == null),
            Latest = await mine.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(5).ToListAsync()
        });
    }
}
