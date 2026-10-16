namespace SupportDesk.Application.Configuration;

/// <summary>
/// Configuration options for issuing and validating JWT authentication tokens (AU-7).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NorthwindSupport";

    public string Audience { get; set; } = "NorthwindSupportWeb";

    public string Key { get; set; } = "NorthwindSupport_Development_Secret_Key_At_Least_32_Characters_Long!";

    /// <summary>Token lifetime in minutes (default 480 min = 8 hours, matching an agent work shift).</summary>
    public int ExpiryMinutes { get; set; } = 480;
}
