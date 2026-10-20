using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Admin)]
public class TrainerAssignmentsController(
    ApplicationDbContext db,
    NotificationService notifications,
    ILogger<TrainerAssignmentsController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var clients = await db.UsersInRole(Roles.Client)
            .OrderBy(u => u.FullName)
            .Select(u => new AssignmentRow(u.Id, u.FullName, u.Email!, u.TrainerId, u.Trainer != null ? u.Trainer.FullName : null))
            .ToListAsync();

        var trainers = await db.UsersInRole(Roles.Trainer)
            .OrderBy(u => u.FullName)
            .Select(t => new TrainerLoad(t.Id, t.FullName, db.Users.Count(c => c.TrainerId == t.Id)))
            .ToListAsync();

        return View(new TrainerAssignmentsViewModel { Clients = clients, Trainers = trainers });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(string clientId, string? trainerId)
    {
        var client = await db.UsersInRole(Roles.Client).FirstOrDefaultAsync(u => u.Id == clientId);
        if (client is null)
            return NotFound();

        trainerId = string.IsNullOrEmpty(trainerId) ? null : trainerId;
        string? trainerName = null;
        if (trainerId is not null)
        {
            trainerName = await db.UsersInRole(Roles.Trainer).Where(t => t.Id == trainerId).Select(t => t.FullName).FirstOrDefaultAsync();
            if (trainerName is null)
                return BadRequest();
        }

        var changed = client.TrainerId != trainerId;
        client.TrainerId = trainerId;
        await db.SaveChangesAsync();
        logger.LogInformation("Client {ClientId} assigned to trainer {TrainerId}", clientId, trainerId ?? "none");

        if (changed && trainerId is not null)
        {
            await notifications.NotifyAsync(client.Id, NotificationType.TrainerAssigned, $"{trainerName} is now your trainer",
                $"{trainerName} will look after your workout and diet plans and can see your progress.", "/Dashboard");
            await notifications.NotifyAsync(trainerId, NotificationType.TrainerAssigned, "New client assigned",
                $"{client.FullName} has been assigned to you.", $"/Clients/Details/{client.Id}");
        }

        TempData["Success"] = trainerName is null
            ? $"{client.FullName} no longer has a trainer."
            : $"{client.FullName} is now assigned to {trainerName}.";
        return RedirectToAction(nameof(Index));
    }
}
