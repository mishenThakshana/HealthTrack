namespace HealthTrack.Helpers;

public static class RateLimits
{
    // 10 posts per minute per IP for sign-in, registration and password flows.
    public const string Auth = "auth";
}
