using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// A user's profile picture (1 : 1 with ApplicationUser). Kept in its own table so loading a user doesn't load the image.
public class ProfilePhoto
{
    [Key]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required]
    public byte[] Data { get; set; } = [];

    [Required, MaxLength(20)]
    public string ContentType { get; set; } = string.Empty;
}
