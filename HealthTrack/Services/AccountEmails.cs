using HealthTrack.Models;

namespace HealthTrack.Services;

public class AccountEmails(EmailQueue emails)
{
    public void SendConfirmation(ApplicationUser user, string email, string url)
    {
        var (html, text) = EmailTemplates.Render(user.FullName, "Confirm your email",
            ["Please confirm this email address so HealthTrack can send you reminders and updates."],
            "Confirm email", url, "If you didn't create a HealthTrack account or change your email, you can ignore this message.");
        emails.Enqueue(new EmailMessage(email, user.FullName, "Confirm your HealthTrack email", html, text));
    }

    public void SendPasswordReset(ApplicationUser user, string url)
    {
        var (html, text) = EmailTemplates.Render(user.FullName, "Reset your password",
            ["We received a request to reset your HealthTrack password. The link works once and expires within a day."],
            "Choose a new password", url, "If you didn't ask for this, you can ignore this email. Your password won't change.");
        emails.Enqueue(new EmailMessage(user.Email!, user.FullName, "Reset your HealthTrack password", html, text));
    }

    public void SendPasswordChanged(ApplicationUser user)
    {
        var (html, text) = EmailTemplates.Render(user.FullName, "Your password was changed",
            [$"The password for your HealthTrack account was changed on {DateTime.Now:d MMM yyyy 'at' HH:mm}.",
             "If this wasn't you, reset your password straight away and contact an administrator."]);
        emails.Enqueue(new EmailMessage(user.Email!, user.FullName, "Your HealthTrack password was changed", html, text));
    }

    public void SendEmailChanged(ApplicationUser user, string oldEmail, string newEmail)
    {
        var (html, text) = EmailTemplates.Render(user.FullName, "Your sign-in email was changed",
            [$"Your HealthTrack sign-in email was changed to {newEmail}.",
             "If this wasn't you, contact an administrator straight away."]);
        emails.Enqueue(new EmailMessage(oldEmail, user.FullName, "Your HealthTrack email was changed", html, text));
    }
}
