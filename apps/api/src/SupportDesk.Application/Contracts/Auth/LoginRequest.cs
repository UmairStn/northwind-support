namespace SupportDesk.Application.Contracts.Auth;

/// <summary>
/// Body of POST /api/auth/login (AU-4).
/// </summary>
public sealed record LoginRequest(string Email, string Password);
