namespace HealthTrack.Helpers;

public static class AccountStatus
{
    public static readonly DateTimeOffset DisabledUntil = DateTimeOffset.MaxValue;

    public static bool IsDisabled(DateTimeOffset? lockoutEnd) => lockoutEnd is { } end && end > DateTimeOffset.UtcNow.AddYears(50);

    public static bool IsLockedOut(DateTimeOffset? lockoutEnd) => lockoutEnd is { } end && end > DateTimeOffset.UtcNow;

    public static string Label(DateTimeOffset? lockoutEnd) =>
        IsDisabled(lockoutEnd) ? "Disabled" : IsLockedOut(lockoutEnd) ? "Locked" : "Active";

    public static string Badge(DateTimeOffset? lockoutEnd) =>
        IsDisabled(lockoutEnd) ? "ht-badge-inactive" : IsLockedOut(lockoutEnd) ? "ht-badge-pending" : "ht-badge-active";
}
