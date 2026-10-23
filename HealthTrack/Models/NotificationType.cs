namespace HealthTrack.Models;

public enum NotificationType
{
    AppointmentRequested,
    AppointmentApproved,
    AppointmentDeclined,
    AppointmentRescheduled,
    AppointmentCancelled,
    AppointmentCompleted,
    AppointmentReminder,
    WorkoutReminder,
    MealReminder,
    PlanAssigned,
    FeedbackReceived,
    TrainerAssigned,
    ProfessionalChosen,
    ClientLeft,
    ReviewReceived
}
