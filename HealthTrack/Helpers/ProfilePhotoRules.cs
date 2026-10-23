namespace HealthTrack.Helpers;

public static class ProfilePhotoRules
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string Accept = "image/jpeg,image/png,image/webp";

    // Works out the image type from the file's first bytes, so a renamed file or a forged Content-Type header is rejected.
    public static string? DetectContentType(ReadOnlySpan<byte> data) =>
        data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ? "image/jpeg"
        : data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? "image/png"
        : data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8) ? "image/webp"
        : null;

    // Returns an error message, or null when the upload is a usable picture.
    public static string? Validate(IFormFile? file) => file switch
    {
        null or { Length: 0 } => "Please choose a picture to upload.",
        { Length: > MaxBytes } => "That picture is too big. Please choose one under 2 MB.",
        _ => null
    };
}
