using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

// Shared by registration and My account: validates and saves the details that belong to a user's role.
internal static class RoleProfiles
{
    public const string ClientPrefix = "Client";
    public const string TrainerPrefix = "Trainer";
    public const string ProfessionalPrefix = "Professional";
    public const int MaxQualifications = 10;

    public static List<string> CleanQualifications(IEnumerable<string?> values) =>
        values.Select(TrackingRules.Clean).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // Only the section for the chosen role is validated; the others are ignored.
    public static void Validate(ModelStateDictionary modelState, string role,
        ClientProfileFormViewModel client, TrainerProfileFormViewModel trainer, ProfessionalProfileFormViewModel professional)
    {
        foreach (var (prefix, owner) in new[] { (ClientPrefix, Roles.Client), (TrainerPrefix, Roles.Trainer), (ProfessionalPrefix, Roles.Professional) })
        {
            if (owner != role)
                RemovePrefix(modelState, prefix);
        }

        if (role == Roles.Client)
        {
            Check(modelState, $"{ClientPrefix}.{nameof(client.FitnessGoal)}", client.FitnessGoal, ProfileOptions.FitnessGoals);
            Check(modelState, $"{ClientPrefix}.{nameof(client.ActivityLevel)}", client.ActivityLevel, ProfileOptions.ActivityLevels);
        }
        else if (role == Roles.Trainer)
        {
            Check(modelState, $"{TrainerPrefix}.{nameof(trainer.Specialization)}", trainer.Specialization, ProfileOptions.Specializations);
            var key = $"{TrainerPrefix}.{nameof(trainer.Qualifications)}";
            var qualifications = CleanQualifications(trainer.Qualifications);
            if (qualifications.Count == 0)
                modelState.AddModelError(key, "Add at least one qualification.");
            else if (qualifications.Count > MaxQualifications)
                modelState.AddModelError(key, $"Add up to {MaxQualifications} qualifications.");
            else if (qualifications.Any(q => q.Length is < 2 or > 100))
                modelState.AddModelError(key, "Each qualification needs 2 to 100 characters.");
        }
        else if (role == Roles.Professional)
            Check(modelState, $"{ProfessionalPrefix}.{nameof(professional.Profession)}", professional.Profession, ProfileOptions.Professions);
    }

    public static void RemovePrefix(ModelStateDictionary modelState, string prefix)
    {
        foreach (var key in modelState.Keys.Where(k => k.StartsWith(prefix + ".", StringComparison.Ordinal)).ToList())
            modelState.Remove(key);
    }

    // Call after the user exists; the caller saves the changes.
    public static async Task ApplyAsync(ApplicationDbContext db, string userId, string role,
        ClientProfileFormViewModel client, TrainerProfileFormViewModel trainer, ProfessionalProfileFormViewModel professional)
    {
        if (role == Roles.Client)
        {
            var profile = await db.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == userId) ?? db.ClientProfiles.Add(new ClientProfile { UserId = userId }).Entity;
            profile.FitnessGoal = client.FitnessGoal!;
            profile.ActivityLevel = client.ActivityLevel!;
            profile.HealthConditions = TrackingRules.Clean(client.HealthConditions);
            profile.UpdatedAt = DateTime.UtcNow;
        }
        else if (role == Roles.Trainer)
        {
            var profile = await db.TrainerProfiles.Include(p => p.Qualifications).FirstOrDefaultAsync(p => p.UserId == userId)
                ?? db.TrainerProfiles.Add(new TrainerProfile { UserId = userId }).Entity;
            profile.Specialization = trainer.Specialization!;
            profile.YearsOfExperience = trainer.YearsOfExperience!.Value;
            profile.Qualifications.Clear();
            profile.Qualifications.AddRange(CleanQualifications(trainer.Qualifications).Select(q => new TrainerQualification { Name = q }));
            profile.UpdatedAt = DateTime.UtcNow;
        }
        else if (role == Roles.Professional)
        {
            var profile = await db.ProfessionalProfiles.FirstOrDefaultAsync(p => p.UserId == userId) ?? db.ProfessionalProfiles.Add(new ProfessionalProfile { UserId = userId }).Entity;
            profile.Profession = professional.Profession!;
            profile.RegistrationNumber = professional.RegistrationNumber!.Trim();
            profile.Workplace = TrackingRules.Clean(professional.Workplace);
            profile.YearsOfExperience = professional.YearsOfExperience!.Value;
            profile.UpdatedAt = DateTime.UtcNow;
        }
    }

    // Removes details kept for roles the user no longer has, for example after an admin changes their role.
    public static async Task RemoveOtherRolesAsync(ApplicationDbContext db, string userId, string role)
    {
        if (role != Roles.Client)
            await db.ClientProfiles.Where(p => p.UserId == userId).ExecuteDeleteAsync();
        if (role != Roles.Trainer)
            await db.TrainerProfiles.Where(p => p.UserId == userId).ExecuteDeleteAsync();
        if (role != Roles.Professional)
            await db.ProfessionalProfiles.Where(p => p.UserId == userId).ExecuteDeleteAsync();
    }

    public static async Task<(ClientProfileFormViewModel, TrainerProfileFormViewModel, ProfessionalProfileFormViewModel)> LoadFormsAsync(
        ApplicationDbContext db, string userId) =>
    (
        await db.ClientProfiles.Where(p => p.UserId == userId)
            .Select(p => new ClientProfileFormViewModel { FitnessGoal = p.FitnessGoal, ActivityLevel = p.ActivityLevel, HealthConditions = p.HealthConditions })
            .FirstOrDefaultAsync() ?? new(),
        await db.TrainerProfiles.Where(p => p.UserId == userId)
            .Select(p => new TrainerProfileFormViewModel
            {
                Specialization = p.Specialization,
                YearsOfExperience = p.YearsOfExperience,
                Qualifications = p.Qualifications.OrderBy(q => q.Id).Select(q => (string?)q.Name).ToList()
            })
            .FirstOrDefaultAsync() ?? new(),
        await db.ProfessionalProfiles.Where(p => p.UserId == userId)
            .Select(p => new ProfessionalProfileFormViewModel
            {
                Profession = p.Profession, RegistrationNumber = p.RegistrationNumber, Workplace = p.Workplace, YearsOfExperience = p.YearsOfExperience
            })
            .FirstOrDefaultAsync() ?? new()
    );

    private static void Check(ModelStateDictionary modelState, string key, string? value, string[] allowed)
    {
        if (value is not null && !allowed.Contains(value))
            modelState.AddModelError(key, "Please choose a valid option.");
    }
}
