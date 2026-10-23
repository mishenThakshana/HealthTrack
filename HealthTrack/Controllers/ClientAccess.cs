using HealthTrack.Data;
using HealthTrack.Models;
using System.Security.Claims;

namespace HealthTrack.Controllers;

internal static class ClientAccess
{
    // Trainers see their assigned clients; nobody else sees another user's client data.
    public static IQueryable<ApplicationUser> VisibleClients(ApplicationDbContext db, ClaimsPrincipal viewer, string viewerId) =>
        viewer.IsInRole(Roles.Trainer)
            ? db.Users.Where(u => u.TrainerId == viewerId)
            : db.Users.Where(_ => false);
}
