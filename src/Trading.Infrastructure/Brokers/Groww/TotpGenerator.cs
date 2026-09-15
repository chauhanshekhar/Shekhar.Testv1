using System.Security.Cryptography;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// RFC 6238 time-based one-time passwords — the same 6-digit code an authenticator app shows.
///
/// Implemented against the framework's HMACSHA1 rather than pulling in a package: the algorithm is
/// ~30 lines and adding a dependency for it would violate the "prefer existing framework
/// capabilities" rule. Correctness is pinned by the RFC's own published test vectors (see tests).
///
/// Used to obtain a Groww access token without a human pasting one in every morning:
///     POST /v1/token/api/access   { "key_type": "totp", "totp": "&lt;code&gt;" }
///
/// SECURITY: the TOTP secret is a long-lived credential equivalent to the second factor on the
/// trading account. It must come from an environment variable or user-secrets, NEVER from a file
/// committed to the repository.
/// </summary>
public static class TotpGenerator
{
    private const int DefaultDigits = 6;
    private const int DefaultPeriodSeconds = 30;

    /// <summary>
    /// Generates the code valid at <paramref name="utcNow"/>.
    /// <paramref name="base32Secret"/> is the shared secret exactly as the authenticator app was
    /// enrolled with — Base32, case-insensitive, '=' padding and spaces tolerated.
    /// </summary>
    public static string Generate(
        string base32Secret,
        DateTime utcNow,
        int digits = DefaultDigits,
        int periodSeconds = DefaultPeriodSeconds)
    {
        if (string.IsNullOrWhiteSpace(base32Secret))
            throw new ArgumentException("TOTP secret is empty", nameof(base32Secret));

        var key = DecodeBase32(base32Secret);
        var counter = ToUnixSeconds(utcNow) / periodSeconds;

        return GenerateFromCounter(key, counter, digits);
    }

    /// <summary>HOTP (RFC 4226) — the counter-based primitive TOTP is built on.</summary>
    public static string GenerateFromCounter(byte[] key, long counter, int digits = DefaultDigits)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);          // RFC 4226 requires big-endian

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        // Dynamic truncation: the low nibble of the last byte picks the 4-byte window.
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var modulo = (int)Math.Pow(10, digits);
        return (binary % modulo).ToString().PadLeft(digits, '0');
    }

    /// <summary>
    /// Seconds remaining in the current window. A code generated with under a second or two left
    /// can expire in flight, so callers should wait for the next window rather than risk it.
    /// </summary>
    public static int SecondsRemaining(DateTime utcNow, int periodSeconds = DefaultPeriodSeconds) =>
        periodSeconds - (int)(ToUnixSeconds(utcNow) % periodSeconds);

    private static long ToUnixSeconds(DateTime utcNow) =>
        (long)(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>RFC 4648 Base32 decode. Tolerates lower case, spaces and '=' padding.</summary>
    internal static byte[] DecodeBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        var cleaned = input.Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(cleaned.Length * 5 / 8);

        int buffer = 0, bitsInBuffer = 0;

        foreach (var c in cleaned)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
                throw new FormatException($"'{c}' is not a valid Base32 character");

            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes.Add((byte)(buffer >> bitsInBuffer));
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        return [.. bytes];
    }
}
