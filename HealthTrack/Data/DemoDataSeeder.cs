using HealthTrack.Helpers;
using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

// Development only: wipes the database and fills it with realistic test data (run with --seed-demo).
// Everything is dated relative to today, so the dashboards, charts and reminders always have recent activity.
public static class DemoDataSeeder
{
    public const string Password = "HealthTrack@2026";

    private sealed record TrainerSeed(string Email, string FullName, string Gender, string Specialization, int Years, string[] Qualifications);

    private sealed record ClientSeed(
        string Email, string FullName, string Gender, DateOnly DateOfBirth, string? TrainerEmail,
        decimal StartWeight, decimal TargetChange, decimal HeightCm, decimal BodyFat, int HeartRate, int ActivityLevel);

    private static readonly TrainerSeed[] Trainers =
    [
        new("nuwan.jayasinghe@healthtrack.local", "Nuwan Jayasinghe", "Male", "Strength and muscle", 8,
            ["BSc (Hons) Sports Science, University of Sri Jayewardenepura", "ACE Certified Personal Trainer"]),
        new("dilani.wijesinghe@healthtrack.local", "Dilani Wijesinghe", "Female", "Nutrition and diet", 6,
            ["BSc Food Science and Nutrition, University of Peradeniya", "ISSN Certified Sports Nutritionist"]),
        new("pradeep.gunawardena@healthtrack.local", "Pradeep Gunawardena", "Male", "Cardio and endurance", 10,
            ["Diploma in Physical Education, National Institute of Education", "NASM Certified Personal Trainer", "First Aid and CPR, Sri Lanka Red Cross"]),
        new("sachini.rathnayake@healthtrack.local", "Sachini Rathnayake", "Female", "Yoga and mobility", 4,
            ["Yoga Alliance RYT-200", "Diploma in Physiotherapy Assistance"]),
        new("lahiru.kumarasinghe@healthtrack.local", "Lahiru Kumarasinghe", "Male", "Injury recovery", 1,
            ["BSc (Hons) Physiotherapy, University of Colombo"])
    ];

    private static readonly ClientSeed[] Clients =
    [
        new("kavindu.senanayake@healthtrack.local", "Kavindu Senanayake", "Male", new(1998, 3, 14), "nuwan.jayasinghe@healthtrack.local", 82.4m, -5.6m, 175m, 24.0m, 74, 4),
        new("ishan.bandara@healthtrack.local", "Ishan Bandara", "Male", new(2001, 7, 2), "nuwan.jayasinghe@healthtrack.local", 68.0m, 2.4m, 178m, 14.5m, 66, 3),
        new("chathura.ekanayake@healthtrack.local", "Chathura Ekanayake", "Male", new(1990, 11, 23), "nuwan.jayasinghe@healthtrack.local", 91.2m, -3.1m, 172m, 29.0m, 78, 2),
        new("tharushi.herath@healthtrack.local", "Tharushi Herath", "Female", new(1996, 5, 9), "dilani.wijesinghe@healthtrack.local", 71.5m, -4.2m, 160m, 31.0m, 76, 4),
        new("nethmi.dissanayake@healthtrack.local", "Nethmi Dissanayake", "Female", new(2000, 1, 30), "dilani.wijesinghe@healthtrack.local", 58.3m, -0.8m, 157m, 26.0m, 70, 3),
        new("ravindu.karunaratne@healthtrack.local", "Ravindu Karunaratne", "Male", new(1984, 9, 17), "dilani.wijesinghe@healthtrack.local", 88.7m, -2.9m, 169m, 30.5m, 80, 2),
        new("sanduni.weerasinghe@healthtrack.local", "Sanduni Weerasinghe", "Female", new(1993, 4, 21), "pradeep.gunawardena@healthtrack.local", 66.8m, -3.5m, 162m, 30.0m, 72, 3),
        new("dinuka.liyanage@healthtrack.local", "Dinuka Liyanage", "Male", new(1999, 12, 5), "pradeep.gunawardena@healthtrack.local", 74.1m, -1.2m, 181m, 18.0m, 64, 4),
        new("hiruni.amarasinghe@healthtrack.local", "Hiruni Amarasinghe", "Female", new(1995, 8, 12), "sachini.rathnayake@healthtrack.local", 55.9m, 0.4m, 158m, 23.5m, 68, 2),
        new("malsha.hettiarachchi@healthtrack.local", "Malsha Hettiarachchi", "Female", new(1988, 2, 27), "sachini.rathnayake@healthtrack.local", 63.4m, -1.0m, 155m, 28.0m, 73, 1),
        new("sahan.kumarasinghe@healthtrack.local", "Sahan Kumarasinghe", "Male", new(2002, 6, 18), null, 79.0m, 0m, 176m, 22.0m, 70, 1),
        new("pavithra.samarasinghe@healthtrack.local", "Pavithra Samarasinghe", "Female", new(1997, 10, 3), null, 61.2m, 0m, 163m, 27.0m, 71, 0),
        new("dimuthu.senarath@healthtrack.local", "Dimuthu Senarath", "Male", new(1992, 3, 8), null, 85.0m, 0m, 174m, 26.0m, 75, 0)
    ];

    private static readonly (string Name, int CaloriesPerMinute)[] Activities =
    [
        ("Brisk walk at Viharamahadevi Park", 5), ("Treadmill run", 10), ("Cycling", 8), ("Swimming", 9),
        ("Upper body strength", 6), ("Lower body strength", 7), ("Yoga flow", 4), ("Badminton", 7), ("HIIT circuit", 11)
    ];

    private static readonly (MealType Type, string Description, int Calories)[] Meals =
    [
        (MealType.Breakfast, "String hoppers with kiri hodi and a boiled egg", 420),
        (MealType.Breakfast, "Kiribath with lunu miris", 480),
        (MealType.Breakfast, "Oats with banana and milk", 380),
        (MealType.Lunch, "Red rice with chicken curry, dhal and gotukola sambol", 650),
        (MealType.Lunch, "Red rice with fish ambul thiyal and mallum", 620),
        (MealType.Lunch, "Vegetable fried rice with devilled tofu", 590),
        (MealType.Dinner, "Kurakkan roti with katta sambol", 450),
        (MealType.Dinner, "Hoppers with egg and seeni sambol", 520),
        (MealType.Dinner, "Grilled chicken with steamed vegetables", 480),
        (MealType.Snack, "King coconut water and a handful of cashews", 210),
        (MealType.Snack, "Mung bean kiribath (mung kiribath) slice", 230),
        (MealType.Snack, "Papaya and pineapple fruit bowl", 150)
    ];

    public static async Task ResetAndSeedAsync(IServiceProvider services)
    {
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        // Recreates the schema, the roles and the configured admin account.
        await DbSeeder.SeedAsync(services);

        using (var scope = services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            await SeedAsync(provider.GetRequiredService<ApplicationDbContext>(), provider.GetRequiredService<UserManager<ApplicationUser>>());
        }
    }

    private static async Task SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        var random = new Random(2026);
        var today = Dates.Today;
        var now = DateTime.Now;

        var admin2 = await CreateUserAsync(userManager, "ruwani.jayawardena@healthtrack.local", "Ruwani Jayawardena", null, null, Roles.Admin, now.AddDays(-118));
        admin2.LastLoginAt = DateTime.UtcNow.AddDays(-2);
        await userManager.UpdateAsync(admin2);

        var trainers = new Dictionary<string, ApplicationUser>();
        foreach (var (t, i) in Trainers.Select((t, i) => (t, i)))
        {
            var user = await CreateUserAsync(userManager, t.Email, t.FullName, t.Gender, null, Roles.Trainer, now.AddDays(-115 + i * 4));
            user.LastLoginAt = DateTime.UtcNow.AddHours(-(i * 9 + 3));
            await userManager.UpdateAsync(user);
            db.TrainerProfiles.Add(new TrainerProfile
            {
                UserId = user.Id, Specialization = t.Specialization, YearsOfExperience = t.Years,
                Qualifications = t.Qualifications.Select(q => new TrainerQualification { Name = q }).ToList()
            });
            trainers[t.Email] = user;
        }
        await db.SaveChangesAsync();

        var clients = new List<(ClientSeed Seed, ApplicationUser User)>();
        foreach (var (c, i) in Clients.Select((c, i) => (c, i)))
        {
            // The last three clients joined this week; the others over the last few months.
            var joined = i >= Clients.Length - 3 ? now.AddDays(-(i - Clients.Length + 4) * 2) : now.AddDays(-100 + i * 3);
            var user = await CreateUserAsync(userManager, c.Email, c.FullName, c.Gender, c.DateOfBirth, Roles.Client, joined);
            user.TrainerId = c.TrainerEmail is null ? null : trainers[c.TrainerEmail].Id;
            var lastLogin = DateTime.UtcNow.AddHours(-(i * 7 + 1));
            user.LastLoginAt = c.ActivityLevel == 0 ? null : lastLogin < user.CreatedAt ? user.CreatedAt.AddHours(1) : lastLogin;
            await userManager.UpdateAsync(user);
            clients.Add((c, user));
        }

        // An account an administrator has disabled, so the Users page shows that status.
        var disabled = clients.Single(c => c.Seed.Email.StartsWith("dimuthu")).User;
        await userManager.SetLockoutEnabledAsync(disabled, true);
        await userManager.SetLockoutEndDateAsync(disabled, AccountStatus.DisabledUntil);

        foreach (var (seed, user) in clients.Where(c => c.Seed.ActivityLevel > 0))
            SeedTracking(db, random, seed, user, today, DateOnly.FromDateTime(user.CreatedAt.ToLocalTime()));
        await db.SaveChangesAsync();

        foreach (var (seed, user) in clients.Where(c => c.Seed.TrainerEmail is not null))
            SeedPlans(db, trainers[seed.TrainerEmail!], user, today, seed);
        await db.SaveChangesAsync();

        SeedAppointments(db, random, clients, trainers, today);
        await db.SaveChangesAsync();
        SeedReviews(db, random);
        SeedFeedback(db, clients, trainers);
        await db.SaveChangesAsync();
        SeedNotifications(db, clients, trainers);
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager,
        string email, string fullName, string? gender, DateOnly? dateOfBirth, string role, DateTime createdAt)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, Gender = gender,
            DateOfBirth = dateOfBirth, CreatedAt = createdAt.ToUniversalTime(), PrivacyAcceptedAt = createdAt.ToUniversalTime()
        };
        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to seed {email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static DateTime At(DateOnly date, int hour, int minute = 0) => date.ToDateTime(new TimeOnly(hour, minute)).ToUniversalTime();

    // Measurements twice a week with a steady trend, workouts on most days and meals on almost every day.
    private static void SeedTracking(ApplicationDbContext db, Random random, ClientSeed seed, ApplicationUser user, DateOnly today, DateOnly joined)
    {
        const int days = 100;
        var start = today.AddDays(-days) > joined ? today.AddDays(-days) : joined;
        var dailyWorkoutChance = seed.ActivityLevel switch { 4 => 0.7, 3 => 0.55, 2 => 0.4, _ => 0.2 };
        var favourites = Activities.OrderBy(_ => random.Next()).Take(4).ToArray();

        for (var date = start; date <= today; date = date.AddDays(1))
        {
            var progress = (date.DayNumber - start.DayNumber) / (decimal)days;

            if (date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday || date == today.AddDays(-1))
            {
                var weight = Math.Round(seed.StartWeight + seed.TargetChange * progress + (decimal)(random.NextDouble() - 0.5) * 0.6m, 1);
                db.HealthMeasurements.Add(new HealthMeasurement
                {
                    ClientId = user.Id, Date = date, WeightKg = weight,
                    HeightCm = date.DayNumber - start.DayNumber < 4 ? seed.HeightCm : null,
                    BodyFatPercent = Math.Round(seed.BodyFat + seed.TargetChange * 0.45m * progress + (decimal)(random.NextDouble() - 0.5) * 0.4m, 1),
                    RestingHeartRate = seed.HeartRate - (int)Math.Round(6 * progress) + random.Next(-1, 2),
                    Notes = date.DayOfWeek == DayOfWeek.Monday && random.NextDouble() < 0.3 ? "Measured before breakfast" : null,
                    CreatedAt = At(date, 7, random.Next(0, 50))
                });
            }

            if (date < today && random.NextDouble() < dailyWorkoutChance)
            {
                var (name, perMinute) = random.NextDouble() < 0.8 ? favourites[random.Next(favourites.Length)] : Activities[random.Next(Activities.Length)];
                var minutes = random.Next(4, 13) * 5;
                db.WorkoutLogs.Add(new WorkoutLog
                {
                    ClientId = user.Id, Date = date, Activity = name, DurationMinutes = minutes,
                    CaloriesBurned = minutes * perMinute + random.Next(-15, 16),
                    Notes = random.NextDouble() < 0.15 ? "Felt strong today" : null,
                    CreatedAt = At(date, 18, random.Next(0, 59))
                });
            }

            if (date < today && random.NextDouble() < 0.35 + seed.ActivityLevel * 0.15)
            {
                foreach (var type in new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner, MealType.Snack })
                {
                    if (type == MealType.Snack && random.NextDouble() < 0.5)
                        continue;
                    var options = Meals.Where(m => m.Type == type).ToArray();
                    var meal = options[random.Next(options.Length)];
                    db.MealLogs.Add(new MealLog
                    {
                        ClientId = user.Id, Date = date, MealType = type, Description = meal.Description,
                        Calories = meal.Calories + random.Next(-30, 31),
                        CreatedAt = At(date, type switch { MealType.Breakfast => 8, MealType.Lunch => 13, MealType.Snack => 16, _ => 20 }, random.Next(0, 59))
                    });
                }
            }
        }
    }

    private static void SeedPlans(ApplicationDbContext db, ApplicationUser trainer, ApplicationUser client, DateOnly today, ClientSeed seed)
    {
        var isStrength = seed.TargetChange > 0 || seed.Email.StartsWith("kavindu");
        var current = new WorkoutPlan
        {
            TrainerId = trainer.Id, ClientId = client.Id,
            Title = isStrength ? "Strength block - Phase 2" : "Fat loss and conditioning",
            Description = isStrength
                ? "Four sessions a week. Increase the load by 2.5 kg once all sets feel easy. Rest 90 seconds between sets."
                : "Mix of steady cardio and full body circuits. Keep the heart rate around 130 to 150 bpm.",
            StartDate = today.AddDays(-27), EndDate = today.AddDays(29), IsActive = true,
            CreatedAt = At(today.AddDays(-28), 10),
            Exercises = isStrength
                ?
                [
                    new() { Day = DayOfWeek.Monday, Name = "Barbell back squat", Sets = 4, Reps = 8, DurationMinutes = 20 },
                    new() { Day = DayOfWeek.Monday, Name = "Romanian deadlift", Sets = 3, Reps = 10, DurationMinutes = 15 },
                    new() { Day = DayOfWeek.Wednesday, Name = "Bench press", Sets = 4, Reps = 8, DurationMinutes = 20 },
                    new() { Day = DayOfWeek.Wednesday, Name = "Seated cable row", Sets = 3, Reps = 12, DurationMinutes = 15 },
                    new() { Day = DayOfWeek.Friday, Name = "Upper body strength", Sets = 4, Reps = 10, DurationMinutes = 40, Notes = "Overhead press, pull-ups and dips" },
                    new() { Day = DayOfWeek.Friday, Name = "Plank holds", Sets = 3, Reps = 1, Notes = "Hold for 45 seconds" },
                    new() { Day = DayOfWeek.Saturday, Name = "Treadmill run", DurationMinutes = 30, Notes = "Easy pace, conversational" },
                    new() { Day = DayOfWeek.Sunday, Name = "Brisk walk at Viharamahadevi Park", DurationMinutes = 40 }
                ]
                :
                [
                    new() { Day = DayOfWeek.Monday, Name = "Brisk walk at Viharamahadevi Park", DurationMinutes = 45 },
                    new() { Day = DayOfWeek.Tuesday, Name = "HIIT circuit", Sets = 4, Reps = 12, DurationMinutes = 25 },
                    new() { Day = DayOfWeek.Wednesday, Name = "Cycling", DurationMinutes = 40 },
                    new() { Day = DayOfWeek.Friday, Name = "Lower body strength", Sets = 3, Reps = 12, DurationMinutes = 35 },
                    new() { Day = DayOfWeek.Friday, Name = "Yoga flow", DurationMinutes = 20, Notes = "Cool down and stretch" },
                    new() { Day = DayOfWeek.Saturday, Name = "Swimming", DurationMinutes = 30 }
                ]
        };
        var previous = new WorkoutPlan
        {
            TrainerId = trainer.Id, ClientId = client.Id, Title = "Foundation phase",
            Description = "Learning the main movements with light weights.",
            StartDate = today.AddDays(-90), EndDate = today.AddDays(-28), IsActive = false, CreatedAt = At(today.AddDays(-91), 11),
            Exercises =
            [
                new() { Day = DayOfWeek.Tuesday, Name = "Goblet squat", Sets = 3, Reps = 12, DurationMinutes = 15 },
                new() { Day = DayOfWeek.Thursday, Name = "Push-ups", Sets = 3, Reps = 10, DurationMinutes = 10 },
                new() { Day = DayOfWeek.Saturday, Name = "Brisk walk at Viharamahadevi Park", DurationMinutes = 40 }
            ]
        };
        var target = isStrength ? 2400 : 1700;
        var diet = new DietPlan
        {
            TrainerId = trainer.Id, ClientId = client.Id,
            Title = isStrength ? "High-protein Sri Lankan meal plan" : "Balanced calorie-controlled meal plan",
            Description = "Red rice in place of white rice, plenty of green leaves, and at least 2 litres of water a day. Keep sugar in tea to one teaspoon.",
            DailyCalorieTarget = target, StartDate = today.AddDays(-27), IsActive = true, CreatedAt = At(today.AddDays(-28), 10, 30),
            Meals =
            [
                new() { MealType = MealType.Breakfast, Description = "String hoppers with kiri hodi and a boiled egg", Calories = 420 },
                new() { MealType = MealType.Lunch, Description = "Red rice with chicken curry, dhal and gotukola sambol", Calories = 650 },
                new() { MealType = MealType.Snack, Description = "King coconut water and a handful of cashews", Calories = 210 },
                new() { MealType = MealType.Dinner, Description = isStrength ? "Kurakkan roti with katta sambol and grilled fish" : "Grilled chicken with steamed vegetables", Calories = isStrength ? 610 : 480 }
            ]
        };
        db.WorkoutPlans.AddRange(current, previous);
        db.DietPlans.Add(diet);

        // A plan that starts next week, so the dashboard shows what's coming.
        if (seed.Email.StartsWith("kavindu"))
            db.WorkoutPlans.Add(new WorkoutPlan
            {
                TrainerId = trainer.Id, ClientId = client.Id, Title = "Strength block - Phase 3 (deload week)",
                Description = "Lighter week before the next block.", StartDate = today.AddDays(30), EndDate = today.AddDays(36), IsActive = true,
                CreatedAt = At(today.AddDays(-2), 15),
                Exercises = [new() { Day = DayOfWeek.Monday, Name = "Barbell back squat", Sets = 3, Reps = 6, DurationMinutes = 15 }]
            });
    }

    private static void SeedAppointments(ApplicationDbContext db, Random random,
        List<(ClientSeed Seed, ApplicationUser User)> clients, Dictionary<string, ApplicationUser> trainers, DateOnly today)
    {
        string[] reasons =
        [
            "Monthly progress review and plan update", "Knee pain when squatting, would like to check my form",
            "Advice on meals during Avurudu season", "Want to start training for the Colombo marathon",
            "Review my diet plan, I feel tired in the afternoons", "Help with a home workout routine for rainy days",
            "Discuss blood sugar readings and suitable meals", "Questions about protein intake"
        ];

        foreach (var (seed, client) in clients.Where(c => c.Seed.TrainerEmail is not null))
        {
            var trainer = trainers[seed.TrainerEmail!];
            var offset = random.Next(0, 3);

            // Past appointments, every three weeks or so, most of them completed.
            for (var weeksAgo = 12; weeksAgo >= 2; weeksAgo -= 3)
            {
                var date = today.AddDays(-weeksAgo * 7 + offset);
                var status = weeksAgo == 6 && random.NextDouble() < 0.4 ? AppointmentStatus.Cancelled : AppointmentStatus.Completed;
                Add(db, client, trainer, date, 9 + random.Next(0, 9), 60, (AppointmentMode)random.Next(0, 3),
                    reasons[random.Next(reasons.Length)], status, cancelledByClient: status == AppointmentStatus.Cancelled);
            }

            // One upcoming approved session and, for some clients, a request waiting for an answer.
            Add(db, client, trainer, today.AddDays(2 + random.Next(0, 6)), 8 + random.Next(0, 10), 45, AppointmentMode.InPerson,
                "Monthly progress review and plan update", AppointmentStatus.Approved, location: "HealthTrack Studio, Colombo 07");
            if (random.NextDouble() < 0.6)
                Add(db, client, trainer, today.AddDays(9 + random.Next(0, 10)), 7 + random.Next(0, 12), 30, AppointmentMode.Video,
                    reasons[random.Next(reasons.Length)], AppointmentStatus.Pending);
        }

        // Fixed appointments for the main demo client and trainer, for the walkthrough and test cases.
        var kavindu = clients.Single(c => c.Seed.Email.StartsWith("kavindu")).User;
        var nuwan = trainers["nuwan.jayasinghe@healthtrack.local"];
        Add(db, kavindu, nuwan, today.AddDays(-9), 17, 30, AppointmentMode.Phone, "Shoulder discomfort during bench press",
            AppointmentStatus.Declined, note: "I'm away at a coaching course that day. Please book another time.");
        Add(db, kavindu, nuwan, today, 6, 60, AppointmentMode.InPerson, "Deadlift technique session", AppointmentStatus.Completed,
            location: "HealthTrack Studio, Colombo 07");
        Add(db, kavindu, nuwan, today.AddDays(1), 10, 45, AppointmentMode.Video, "Check-in on the new strength block", AppointmentStatus.Approved,
            location: "Google Meet link sent by email");

        // An approved session that has already happened but hasn't been marked as completed yet.
        var ishan = clients.Single(c => c.Seed.Email.StartsWith("ishan")).User;
        Add(db, ishan, nuwan, today.AddDays(-2), 15, 45, AppointmentMode.InPerson, "Form check on deadlifts", AppointmentStatus.Approved,
            location: "HealthTrack Studio, Colombo 07");
    }

    private static void Add(ApplicationDbContext db, ApplicationUser client, ApplicationUser trainer, DateOnly date, int hour, int minutes,
        AppointmentMode mode, string reason, AppointmentStatus status, string? location = null, string? note = null, bool cancelledByClient = false)
    {
        var start = date.ToDateTime(new TimeOnly(hour, 0));
        db.Appointments.Add(new Appointment
        {
            ClientId = client.Id, TrainerId = trainer.Id, StartsAt = start, EndsAt = start.AddMinutes(minutes), Mode = mode,
            Reason = reason, Status = status, Location = location, TrainerNote = note, CancelledByClient = cancelledByClient,
            CreatedAt = start.AddDays(-5).ToUniversalTime(), UpdatedAt = start.AddDays(-4).ToUniversalTime()
        });
    }

    private static void SeedReviews(ApplicationDbContext db, Random random)
    {
        string[] comments =
        [
            "Very clear explanations and a realistic plan. I can feel the difference already.",
            "Helpful session, the meal ideas fit our usual rice and curry at home.",
            "Good session but it started a few minutes late.",
            "Corrected my squat form, no more knee pain. Highly recommended.",
            "Friendly and patient. The breathing exercises helped a lot."
        ];

        // Most completed appointments are rated; a few are left so the dashboard asks the client to rate them.
        var completed = db.Appointments.Local.Where(a => a.Status == AppointmentStatus.Completed).OrderBy(a => a.StartsAt).ToList();
        foreach (var a in completed.Where((_, i) => i % 4 != 3 && completed[i].StartsAt < DateTime.Today))
        {
            var rating = random.NextDouble() < 0.7 ? 5 : random.Next(3, 5);
            db.AppointmentReviews.Add(new AppointmentReview
            {
                AppointmentId = a.Id, ClientId = a.ClientId, ProviderId = a.TrainerId, Rating = rating,
                Comment = random.NextDouble() < 0.75 ? comments[random.Next(comments.Length)] : null,
                CreatedAt = a.EndsAt.AddHours(3).ToUniversalTime(), UpdatedAt = a.EndsAt.AddHours(3).ToUniversalTime()
            });
        }
    }

    private static void SeedFeedback(ApplicationDbContext db, List<(ClientSeed Seed, ApplicationUser User)> clients, Dictionary<string, ApplicationUser> trainers)
    {
        (int DaysAgo, string Message)[] messages =
        [
            (40, "Great start. Your squat depth has improved a lot, keep the chest up on the way down."),
            (19, "Weight is coming down steadily, about half a kilo a week, which is exactly what we want."),
            (5, "Well done on logging every meal this week. Try to add one more serving of vegetables at dinner.")
        ];

        foreach (var (seed, client) in clients.Where(c => c.Seed.TrainerEmail is not null && c.Seed.ActivityLevel >= 2))
        {
            foreach (var (daysAgo, message) in messages)
                db.ProgressFeedback.Add(new ProgressFeedback
                {
                    ClientId = client.Id, AuthorId = trainers[seed.TrainerEmail!].Id, Message = message,
                    CreatedAt = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-3)
                });
        }
    }

    private static void SeedNotifications(ApplicationDbContext db, List<(ClientSeed Seed, ApplicationUser User)> clients, Dictionary<string, ApplicationUser> trainers)
    {
        var kavindu = clients.Single(c => c.Seed.Email.StartsWith("kavindu")).User;
        var nuwan = trainers["nuwan.jayasinghe@healthtrack.local"];
        var tomorrow = db.Appointments.Local.Single(a => a.ClientId == kavindu.Id && a.StartsAt.Date == DateTime.Today.AddDays(1));
        var link = $"/Appointments/Details/{tomorrow.Id}";

        db.Notifications.AddRange(
            new Notification { UserId = kavindu.Id, Type = NotificationType.AppointmentReminder, Title = "Upcoming appointment",
                Message = $"Reminder: your video call appointment with Nuwan Jayasinghe is {tomorrow.StartsAt:dddd d MMM 'at' HH:mm}.",
                Link = link, Key = $"appt-reminder:{tomorrow.Id}:{tomorrow.StartsAt:yyyyMMddHHmm}", CreatedAt = DateTime.UtcNow.AddMinutes(-40) },
            new Notification { UserId = kavindu.Id, Type = NotificationType.FeedbackReceived, Title = "New feedback from Nuwan Jayasinghe",
                Message = "Well done on logging every meal this week. Try to add one more serving of vegetables at dinner.",
                Link = "/Progress#feedback", CreatedAt = DateTime.UtcNow.AddDays(-5).AddHours(-3) },
            new Notification { UserId = kavindu.Id, Type = NotificationType.PlanAssigned, Title = "New workout plan",
                Message = "Nuwan Jayasinghe created the workout plan “Strength block - Phase 3 (deload week)” for you.",
                Link = "/WorkoutPlans", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Notification { UserId = kavindu.Id, Type = NotificationType.AppointmentApproved, Title = "Appointment approved",
                Message = $"Nuwan Jayasinghe approved your appointment on {tomorrow.StartsAt:ddd d MMM 'at' HH:mm}.",
                Link = link, CreatedAt = DateTime.UtcNow.AddDays(-3), ReadAt = DateTime.UtcNow.AddDays(-3) },
            new Notification { UserId = nuwan.Id, Type = NotificationType.AppointmentReminder, Title = "Upcoming appointment",
                Message = $"Reminder: your video call appointment with Kavindu Senanayake is {tomorrow.StartsAt:dddd d MMM 'at' HH:mm}.",
                Link = link, Key = $"appt-reminder:{tomorrow.Id}:{tomorrow.StartsAt:yyyyMMddHHmm}", CreatedAt = DateTime.UtcNow.AddMinutes(-40) },
            new Notification { UserId = nuwan.Id, Type = NotificationType.ReviewReceived, Title = "New review",
                Message = "Ishan Bandara rated your appointment 5 out of 5.", Link = "/Providers/Details/" + nuwan.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-1) });
    }
}
