using SupportDesk.Infrastructure.Services;
using Xunit;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ProducesSaltedHash_DifferentEachTime()
    {
        var password = "SecurePassword123!";

        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        Assert.False(string.IsNullOrWhiteSpace(hash1));
        Assert.False(string.IsNullOrWhiteSpace(hash2));
        Assert.NotEqual(hash1, hash2); // Unique salts
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var password = "CorrectPassword123!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(password, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var password = "CorrectPassword123!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword("WrongPassword123!", hash);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-valid-hash")]
    [InlineData("100000.invalidbase64.invalidbase64")]
    public void VerifyPassword_WithInvalidOrMalformedHash_ReturnsFalse(string malformedHash)
    {
        var isValid = _hasher.VerifyPassword("SomePassword", malformedHash);

        Assert.False(isValid);
    }
}
