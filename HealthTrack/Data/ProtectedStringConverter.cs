using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HealthTrack.Data;

// Encrypts a column with ASP.NET Core Data Protection (AES-256-CBC + HMACSHA256).
// Stored values start with "enc:", so rows written before encryption was added still read correctly.
public class ProtectedStringConverter(IDataProtector protector)
    : ValueConverter<string, string>(v => Protect(protector, v), v => Unprotect(protector, v))
{
    public const string Prefix = "enc:";
    public const string Purpose = "HealthTrack.HealthData.v1";

    private static string Protect(IDataProtector protector, string value) => Prefix + protector.Protect(value);

    private static string Unprotect(IDataProtector protector, string value) =>
        value.StartsWith(Prefix, StringComparison.Ordinal) ? protector.Unprotect(value[Prefix.Length..]) : value;
}
