namespace HealthTrack.Data;

public static class Roles
{
    public const string Client = "Client";
    public const string Trainer = "Trainer";
    public const string Admin = "Admin";

    public static readonly string[] All = [Client, Trainer, Admin];
}
