using HealthTrack.Models;

namespace HealthTrack.Helpers;

public static class AppointmentRules
{
    public static readonly int[] Durations = [30, 45, 60, 90];
    public static readonly TimeOnly OpensAt = new(6, 0);
    public static readonly TimeOnly ClosesAt = new(21, 0);
    public const int MaxDaysAhead = 90;

    public static bool IsUpcoming(Appointment a) => a.StartsAt > DateTime.Now;

    public static bool CanRespond(Appointment a) => a.Status == AppointmentStatus.Pending && IsUpcoming(a);

    public static bool CanReschedule(Appointment a) =>
        a.Status is AppointmentStatus.Pending or AppointmentStatus.Approved && IsUpcoming(a);

    public static bool CanCancel(Appointment a, bool isProvider) =>
        (a.Status == AppointmentStatus.Approved || (a.Status == AppointmentStatus.Pending && !isProvider)) && IsUpcoming(a);

    public static bool CanComplete(Appointment a) => a.Status == AppointmentStatus.Approved && !IsUpcoming(a);

    public static string StatusLabel(Appointment a) => a.Status switch
    {
        AppointmentStatus.Pending when !IsUpcoming(a) => "Expired",
        AppointmentStatus.Pending => "Pending",
        AppointmentStatus.Approved => "Approved",
        AppointmentStatus.Declined => "Declined",
        AppointmentStatus.Cancelled => "Cancelled",
        _ => "Completed"
    };

    public static string Label(this AppointmentMode mode) => mode switch
    {
        AppointmentMode.Video => "Video call",
        AppointmentMode.Phone => "Phone call",
        _ => "In person"
    };

    public static string Icon(this AppointmentMode mode) => mode switch
    {
        AppointmentMode.Video => "bi-camera-video",
        AppointmentMode.Phone => "bi-telephone",
        _ => "bi-geo-alt"
    };
}
