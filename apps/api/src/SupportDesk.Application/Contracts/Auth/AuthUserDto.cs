namespace SupportDesk.Application.Contracts.Auth;

/// <summary>
/// Authenticated user summary returned upon login or via /me (AU-4, AU-5).
/// </summary>
public sealed record AuthUserDto(int Id, string FullName, string Email);
