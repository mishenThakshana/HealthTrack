using HealthTrack.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IDataProtectionProvider dataProtection)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Free-text fields that can hold health details are encrypted at rest.
    public static readonly (Type Entity, string Table, string Column)[] EncryptedColumns =
    [
        (typeof(HealthMeasurement), "HealthMeasurements", nameof(HealthMeasurement.Notes)),
        (typeof(WorkoutLog), "WorkoutLogs", nameof(WorkoutLog.Notes)),
        (typeof(MealLog), "MealLogs", nameof(MealLog.Description)),
        (typeof(Appointment), "Appointments", nameof(Appointment.Reason)),
        (typeof(Appointment), "Appointments", nameof(Appointment.TrainerNote)),
        (typeof(ProgressFeedback), "ProgressFeedback", nameof(Models.ProgressFeedback.Message)),
        (typeof(Notification), "Notifications", nameof(Notification.Message))
    ];

    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();
    public DbSet<DietMeal> DietMeals => Set<DietMeal>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();
    public DbSet<HealthMeasurement> HealthMeasurements => Set<HealthMeasurement>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ProgressFeedback> ProgressFeedback => Set<ProgressFeedback>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public IQueryable<ApplicationUser> UsersInRole(string role) =>
        Users.Where(u => UserRoles.Any(ur => ur.UserId == u.Id && Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Trainer)
            .WithMany()
            .HasForeignKey(u => u.TrainerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<ApplicationUser>(u =>
        {
            u.Property(x => x.EmailAppointments).HasDefaultValue(true).HasSentinel(true);
            u.Property(x => x.EmailReminders).HasDefaultValue(true).HasSentinel(true);
            u.Property(x => x.EmailUpdates).HasDefaultValue(true).HasSentinel(true);
            u.Property(x => x.RemindWorkouts).HasDefaultValue(true).HasSentinel(true);
            u.Property(x => x.RemindMeals).HasDefaultValue(true).HasSentinel(true);
        });

        builder.Entity<Notification>(n =>
        {
            n.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            n.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
            n.HasIndex(x => new { x.UserId, x.Key }).IsUnique().HasFilter("[Key] IS NOT NULL");
        });

        builder.Entity<WorkoutPlan>(plan =>
        {
            plan.HasOne(p => p.Client).WithMany().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.NoAction);
            plan.HasOne(p => p.Trainer).WithMany().HasForeignKey(p => p.TrainerId).OnDelete(DeleteBehavior.NoAction);
            plan.HasMany(p => p.Exercises).WithOne(e => e.WorkoutPlan).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WorkoutExercise>().Property(e => e.Day).HasConversion<string>().HasMaxLength(10);

        builder.Entity<DietPlan>(plan =>
        {
            plan.HasOne(p => p.Client).WithMany().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.NoAction);
            plan.HasOne(p => p.Trainer).WithMany().HasForeignKey(p => p.TrainerId).OnDelete(DeleteBehavior.NoAction);
            plan.HasMany(p => p.Meals).WithOne(m => m.DietPlan).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DietMeal>().Property(m => m.MealType).HasConversion<string>().HasMaxLength(10);
        builder.Entity<MealLog>().Property(m => m.MealType).HasConversion<string>().HasMaxLength(10);

        builder.Entity<WorkoutLog>().HasIndex(l => new { l.ClientId, l.Date });
        builder.Entity<MealLog>().HasIndex(l => new { l.ClientId, l.Date });

        builder.Entity<HealthMeasurement>(m =>
        {
            m.HasIndex(x => new { x.ClientId, x.Date });
            m.Property(x => x.WeightKg).HasPrecision(5, 1);
            m.Property(x => x.HeightCm).HasPrecision(5, 1);
            m.Property(x => x.BodyFatPercent).HasPrecision(4, 1);
            m.Ignore(x => x.Bmi);
        });

        builder.Entity<Appointment>(a =>
        {
            a.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.NoAction);
            a.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.NoAction);
            a.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            a.Property(x => x.Mode).HasConversion<string>().HasMaxLength(10);
            a.HasIndex(x => new { x.TrainerId, x.StartsAt });
            a.HasIndex(x => new { x.ClientId, x.StartsAt });
            a.Ignore(x => x.DurationMinutes);
        });

        builder.Entity<ProgressFeedback>(f =>
        {
            f.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.NoAction);
            f.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.NoAction);
            f.HasIndex(x => new { x.ClientId, x.CreatedAt });
        });

        var converter = new ProtectedStringConverter(dataProtection.CreateProtector(ProtectedStringConverter.Purpose));
        foreach (var (entity, _, column) in EncryptedColumns)
            builder.Entity(entity).Property(column).HasConversion(converter).HasColumnType("nvarchar(max)");
    }
}
