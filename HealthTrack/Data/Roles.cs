namespace HealthTrack.Data;

public static class Roles
{
    public const string Client = "Client";
    public const string Trainer = "Trainer";
    public const string Professional = "Professional";
    public const string Admin = "Admin";

    public const string Providers = Trainer + "," + Professional;

    public static readonly string[] All = [Client, Trainer, Professional, Admin];

    public static string DisplayName(string role) => role == Professional ? "Health professional" : role;
}
