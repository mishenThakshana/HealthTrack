using HealthTrack.Models;

namespace HealthTrack.Helpers;

public static class ProfileOptions
{
    public static readonly string[] FitnessGoals =
        ["Lose weight", "Build muscle", "Improve fitness", "Eat healthier", "Manage a health condition", "Stay healthy"];

    public static readonly string[] ActivityLevels = ["Mostly sitting", "Lightly active", "Active", "Very active"];

    public static readonly string[] Specializations =
        ["Weight loss", "Strength and muscle", "Cardio and endurance", "Yoga and mobility", "Sports performance", "Injury recovery",
         "Nutrition and diet", "General fitness"];

    public static string Experience(int years) => years switch
    {
        0 => "New to the role",
        1 => "1 year's experience",
        _ => $"{years} years' experience"
    };

    // Label/value pairs for whichever role profile is loaded on the user. Health notes are left out.
    public static List<(string Label, string Value)> Details(ApplicationUser user) => user switch
    {
        { ClientProfile: { } c } => [("Main goal", c.FitnessGoal), ("Activity", c.ActivityLevel)],
        { TrainerProfile: { } t } => [("Specialization", t.Specialization), ("Experience", Experience(t.YearsOfExperience)),
            ("Qualifications", string.Join(", ", t.Qualifications.OrderBy(q => q.Id).Select(q => q.Name)))],
        _ => []
    };

    // A one-line summary of a trainer, for example in the appointment booking list.
    public static string? Headline(ApplicationUser user) =>
        user.TrainerProfile is { } t ? $"{t.Specialization} · {Experience(t.YearsOfExperience)}" : null;
}
