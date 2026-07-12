using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Trading.Domain.Entities;

namespace Trading.API.Services;

public static class PasswordSecurity
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(User user, string password)
        => Hasher.HashPassword(user, password);

    public static PasswordVerificationResult Verify(User user, string password)
    {
        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result != PasswordVerificationResult.Failed)
            return result;

        return user.PasswordHash == LegacySha256(password)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }

    private static string LegacySha256(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
}
