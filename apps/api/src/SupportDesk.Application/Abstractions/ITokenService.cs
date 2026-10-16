namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Issues JWT security tokens for authenticated agents (AU-4, AU-7).
/// </summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(int agentId, string fullName, string email);
}
