namespace SupportDesk.Application.Contracts.Auth;

/// <summary>
/// Successful response from POST /api/auth/login (AU-4).
/// </summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, AuthUserDto User);
