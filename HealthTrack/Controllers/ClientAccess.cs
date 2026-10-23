using HealthTrack.Data;
using HealthTrack.Models;
using System.Security.Claims;

namespace HealthTrack.Controllers;

internal static class ClientAccess
{
    // Trainers see their assigned clients. Health professionals see clients who chose them,
    // and clients they have an approved or completed appointment with.
    public static IQueryable<ApplicationUser> VisibleClients(ApplicationDbContext db, ClaimsPrincipal viewer, string viewerId) =>
        viewer.IsInRole(Roles.Professional)
            ? db.UsersInRole(Roles.Client).Where(u => u.ProfessionalId == viewerId || db.Appointments.Any(a => a.ClientId == u.Id && a.TrainerId == viewerId
                && (a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Completed)))
            : viewer.IsInRole(Roles.Trainer)
                ? db.Users.Where(u => u.TrainerId == viewerId)
                : db.Users.Where(_ => false);
}
