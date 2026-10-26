using System.Threading.RateLimiting;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// QuestPDF (PDF report exports) is free under its Community licence for this kind of project.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
if (!isDevelopment && connectionString.Contains("YOUR_PASSWORD"))
    throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection; appsettings.json only holds a placeholder.");

// Keys encrypt auth cookies, tokens and the encrypted health-data columns. Losing them makes that data unreadable, so back them up.
var keysPath = builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } configured
    ? configured
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".healthtrack", "keys");
builder.Services.AddDataProtection()
    .SetApplicationName("HealthTrack")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "HealthTrack.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "HealthTrack.Antiforgery";
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimits.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection(ReminderOptions.Section));
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailSenderService>();
builder.Services.AddScoped<AccountEmails>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AccountDeletion>();
builder.Services.AddHostedService<ReminderService>();

var app = builder.Build();

// "dotnet run -- --seed-demo" (Development only) replaces all data with test data and exits.
if (isDevelopment && args.Contains("--seed-demo"))
{
    await DemoDataSeeder.ResetAndSeedAsync(app.Services);
    return;
}

await DbSeeder.SeedAsync(app.Services);

if (!isDevelopment)
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSecurityHeaders(isDevelopment);
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
