namespace HealthTrack.Models.ViewModels;

// A round profile picture, or the person's initials when they haven't uploaded one. Size is an extra CSS class.
public record Avatar(string UserId, string Name, DateTime? PhotoUpdatedAt, string Size = "")
{
    public static Avatar Of(ApplicationUser user, string size = "") => new(user.Id, user.FullName, user.PhotoUpdatedAt, size);

    public string Initials => string.Concat(Name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .SkipWhile(part => part.EndsWith('.'))
        .Take(2)
        .Select(part => char.ToUpperInvariant(part[0])));
}
