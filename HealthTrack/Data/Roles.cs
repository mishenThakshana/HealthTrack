namespace HealthTrack.Data;

public static class Roles
{
    public const string Client = "Client";
    public const string Trainer = "Trainer";
    public const string Admin = "Admin";

    // Roles that can approve, reschedule, complete and cancel appointments.
    public const string AppointmentManagers = Trainer + "," + Admin;

    public static readonly string[] All = [Client, Trainer, Admin];
}
