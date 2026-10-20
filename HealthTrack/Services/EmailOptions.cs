namespace HealthTrack.Services;

public class EmailOptions
{
    public const string Section = "Email";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@healthtrack.local";
    public string FromName { get; set; } = "HealthTrack";
    public string AppUrl { get; set; } = "http://localhost:5182";
}
