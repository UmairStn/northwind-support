namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords using a secure cryptographic algorithm (AU-2).
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
