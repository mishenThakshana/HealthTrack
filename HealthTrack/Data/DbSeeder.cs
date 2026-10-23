using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

public static class DbSeeder
{
    private sealed record SeedUser(
        string Email, string Password, string FullName, string Role,
        DateOnly? DateOfBirth = null, string? Gender = null, string? TrainerEmail = null,
        string? FitnessGoal = null, string? ActivityLevel = null, string? HealthConditions = null,
        string? Specialization = null, string[]? Qualifications = null,
        string? Profession = null, string? RegistrationNumber = null, string? Workplace = null, int YearsOfExperience = 0);

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await EncryptLegacyRowsAsync(db);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var seedUsers = provider.GetRequiredService<IConfiguration>()
            .GetSection("SeedUsers").Get<SeedUser[]>() ?? [];

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var seed in seedUsers)
            await SeedUserAsync(userManager, seed);

        foreach (var seed in seedUsers.Where(s => s.TrainerEmail is not null))
            await AssignTrainerAsync(userManager, seed);

        foreach (var seed in seedUsers)
            await SeedRoleProfileAsync(db, userManager, seed);
    }

    // Adds the role details a seed user would have entered at registration, if they don't have them yet.
    private static async Task SeedRoleProfileAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager, SeedUser seed)
    {
        if (await userManager.FindByEmailAsync(seed.Email) is not { } user)
            return;

        if (seed.Role == Roles.Client && seed is { FitnessGoal: { } goal, ActivityLevel: { } activity }
            && !await db.ClientProfiles.AnyAsync(p => p.UserId == user.Id))
            db.ClientProfiles.Add(new ClientProfile { UserId = user.Id, FitnessGoal = goal, ActivityLevel = activity, HealthConditions = seed.HealthConditions });
        else if (seed.Role == Roles.Trainer && seed is { Specialization: { } specialization, Qualifications: { Length: > 0 } qualifications }
            && !await db.TrainerProfiles.AnyAsync(p => p.UserId == user.Id))
            db.TrainerProfiles.Add(new TrainerProfile
            {
                UserId = user.Id, Specialization = specialization, YearsOfExperience = seed.YearsOfExperience,
                Qualifications = qualifications.Select(q => new TrainerQualification { Name = q }).ToList()
            });
        else if (seed.Role == Roles.Professional && seed is { Profession: { } profession, RegistrationNumber: { } number }
            && !await db.ProfessionalProfiles.AnyAsync(p => p.UserId == user.Id))
            db.ProfessionalProfiles.Add(new ProfessionalProfile
            {
                UserId = user.Id, Profession = profession, RegistrationNumber = number, Workplace = seed.Workplace, YearsOfExperience = seed.YearsOfExperience
            });
        else
            return;

        await db.SaveChangesAsync();
    }

    // Rows saved before column encryption was added are still plain text; re-saving them encrypts them.
    private static async Task EncryptLegacyRowsAsync(ApplicationDbContext db)
    {
        foreach (var (entity, table, column) in ApplicationDbContext.EncryptedColumns)
        {
            // Table and column names come from the fixed EncryptedColumns list, never from user input.
            var sql = "SELECT [Id] AS [Value] FROM [" + table + "] WHERE [" + column + "] IS NOT NULL AND [" + column + "] NOT LIKE '"
                + ProtectedStringConverter.Prefix + "%'";
            var ids = await db.Database.SqlQueryRaw<int>(sql).ToListAsync();

            foreach (var id in ids)
            {
                if (await db.FindAsync(entity, id) is { } row)
                    db.Entry(row).Property(column).IsModified = true;
            }

            if (ids.Count > 0)
                await db.SaveChangesAsync();
        }
    }

    private static async Task AssignTrainerAsync(UserManager<ApplicationUser> userManager, SeedUser seed)
    {
        var client = await userManager.FindByEmailAsync(seed.Email);
        if (client is null || client.TrainerId is not null)
            return;

        var trainer = await userManager.FindByEmailAsync(seed.TrainerEmail!);
        if (trainer is null || !await userManager.IsInRoleAsync(trainer, Roles.Trainer))
            throw new InvalidOperationException($"Seed user {seed.Email} has unknown trainer '{seed.TrainerEmail}'.");

        client.TrainerId = trainer.Id;
        await userManager.UpdateAsync(client);
    }

    private static async Task SeedUserAsync(UserManager<ApplicationUser> userManager, SeedUser seed)
    {
        if (!Roles.All.Contains(seed.Role))
            throw new InvalidOperationException($"Seed user {seed.Email} has unknown role '{seed.Role}'.");

        var user = await userManager.FindByEmailAsync(seed.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
                FullName = seed.FullName,
                DateOfBirth = seed.DateOfBirth,
                Gender = seed.Gender
            };

            var result = await userManager.CreateAsync(user, seed.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to seed {seed.Email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, seed.Role))
            await userManager.AddToRoleAsync(user, seed.Role);
    }
}
