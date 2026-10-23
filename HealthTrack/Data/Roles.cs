namespace HealthTrack.Data;

public static class Roles
{
    public const string Client = "Client";
    public const string Trainer = "Trainer";
    public const string Professional = "Professional";
    public const string Admin = "Admin";

    public const string Providers = Trainer + "," + Professional;

    // Roles that can approve, reschedule, complete and cancel appointments.
    public const string AppointmentManagers = Providers + "," + Admin;

    public static readonly string[] All = [Client, Trainer, Professional, Admin];

    public static string DisplayName(string role) => role == Professional ? "Health professional" : role;
}
