using System.Text;
using Trading.Infrastructure.Brokers.Groww;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// TOTP correctness, pinned against RFC 6238's OWN published test vectors.
///
/// This matters more than a hand-rolled test would: a wrong code means the daily Groww token
/// refresh fails silently at 06:10 and the system is left on an expired token. Matching the RFC's
/// vectors proves the implementation, not just that it is self-consistent.
/// </summary>
public class TotpGeneratorTests
{
    /// <summary>
    /// RFC 6238 Appendix B uses the ASCII seed "12345678901234567890" with HMAC-SHA1.
    /// The published vectors are 8-digit; the first 6 digits of the 8 are the 6-digit code.
    /// </summary>
    private static readonly byte[] RfcKey = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "94287082")]           // 1970-01-01 00:00:59
    [InlineData(1111111109L, "07081804")]   // 2005-03-18 01:58:29
    [InlineData(1111111111L, "14050471")]   // 2005-03-18 01:58:31
    [InlineData(1234567890L, "89005924")]   // 2009-02-13 23:31:30
    [InlineData(2000000000L, "69279037")]   // 2033-05-18 03:33:20
    public void MatchesRfc6238TestVectors(long unixSeconds, string expectedEightDigits)
    {
        var counter = unixSeconds / 30;

        Assert.Equal(expectedEightDigits, TotpGenerator.GenerateFromCounter(RfcKey, counter, digits: 8));

        // The 6-digit code Groww wants is the low 6 digits of the same truncation.
        var expectedSix = expectedEightDigits[^6..];
        Assert.Equal(expectedSix, TotpGenerator.GenerateFromCounter(RfcKey, counter, digits: 6));
    }

    // ── Base32 decoding of the enrolment secret ─────────────────────────────────────────────

    [Theory]
    [InlineData("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ")]        // canonical
    [InlineData("gezdgnbvgy3tqojqgezdgnbvgy3tqojq")]        // lower case
    [InlineData("GEZD GNBV GY3T QOJQ GEZD GNBV GY3T QOJQ")] // grouped with spaces, as apps display it
    [InlineData("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ====")]    // padded
    public void SecretFormattingVariantsAllDecodeToTheSameKey(string secret)
    {
        // That Base32 string IS the RFC's "12345678901234567890" seed.
        Assert.Equal(RfcKey, TotpGenerator.DecodeBase32(secret));
    }

    [Fact]
    public void GeneratedCodeIsSixDigits()
    {
        var code = TotpGenerator.Generate("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", DateTime.UtcNow);

        Assert.Equal(6, code.Length);
        Assert.True(code.All(char.IsDigit));
    }

    [Fact]
    public void CodeIsStableWithinAWindowAndChangesAcrossOne()
    {
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        var windowStart = new DateTime(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            TotpGenerator.Generate(secret, windowStart),
            TotpGenerator.Generate(secret, windowStart.AddSeconds(29)));

        Assert.NotEqual(
            TotpGenerator.Generate(secret, windowStart),
            TotpGenerator.Generate(secret, windowStart.AddSeconds(30)));
    }

    // ── The near-expiry guard that stops a code dying in flight ─────────────────────────────

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 29)]
    [InlineData(29, 1)]
    [InlineData(30, 30)]
    public void SecondsRemainingTracksTheWindow(int secondsIntoMinute, int expected)
    {
        var t = new DateTime(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc).AddSeconds(secondsIntoMinute);

        Assert.Equal(expected, TotpGenerator.SecondsRemaining(t));
    }

    // ── Configuration errors surface loudly, not as a wrong code ────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptySecretThrows(string secret)
    {
        Assert.Throws<ArgumentException>(() => TotpGenerator.Generate(secret, DateTime.UtcNow));
    }

    [Fact]
    public void InvalidBase32Throws()
    {
        // '1', '8' and '9' are not in the RFC 4648 Base32 alphabet — a typo'd secret must fail
        // loudly rather than silently produce a code that will never authenticate.
        Assert.Throws<FormatException>(() => TotpGenerator.Generate("ABC189!!", DateTime.UtcNow));
    }
}

/// <summary>
/// Guards the credential mix-up that would otherwise fail silently every morning: storing the API
/// SECRET in the TOTP secret slot. The API secret is not Base32, so the TOTP flow can never work
/// with it — the service must detect that and fall back to the checksum flow rather than throwing.
/// </summary>
public class GrowwCredentialFlowTests
{
    [Theory]
    [InlineData("GEZDGNBVGY3TQOJQ")]                    // canonical Base32
    [InlineData("gezd gnbv gy3t qojq")]                 // spaced + lower case, as apps display it
    [InlineData("GEZDGNBVGY3TQOJQ====")]                // padded
    public void ValidBase32IsAcceptedAsATotpSecret(string secret)
    {
        Assert.True(GrowwTokenService.IsUsableTotpSecret(secret));
    }

    [Theory]
    // Shaped like a Groww API SECRET — symbols plus the digits 0/1/8/9, none of them Base32.
    // Deliberately a made-up value: never put a real credential in a source file.
    [InlineData("Xy9pQ#aa1bCDEF0*ghI_jkL^m(nopQr@")]
    [InlineData("abc123")]                              // 1 is not Base32
    [InlineData("secret+value")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NonBase32IsRejectedSoItFallsBackToChecksum(string? secret)
    {
        Assert.False(GrowwTokenService.IsUsableTotpSecret(secret));
    }

    [Fact]
    public void ChecksumIsSha256OfSecretPlusTimestamp()
    {
        // Documented rule: checksum = SHA-256(api_secret + epoch_seconds), lower-case hex.
        var expected = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes("mysecret1719830400")))
            .ToLowerInvariant();

        Assert.Equal(expected, GrowwTokenService.ComputeChecksum("mysecret", 1719830400L));
        Assert.Equal(64, GrowwTokenService.ComputeChecksum("mysecret", 1719830400L).Length);
    }

    [Fact]
    public void ChecksumChangesWithTheTimestamp()
    {
        Assert.NotEqual(
            GrowwTokenService.ComputeChecksum("mysecret", 1719830400L),
            GrowwTokenService.ComputeChecksum("mysecret", 1719830401L));
    }
}
