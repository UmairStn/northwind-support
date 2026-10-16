namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when authentication fails (AU-4). Always yields a generic 401 message to prevent enumeration.
/// </summary>
public sealed class AuthenticationFailedException(string message = "Invalid email or password.")
    : Exception(message);
