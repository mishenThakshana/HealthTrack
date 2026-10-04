using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

public static class DbSeeder
{
    private sealed record SeedUser(
        string Email, string Password, string FullName, string Role, DateOnly? DateOfBirth, string? Gender);

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

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
