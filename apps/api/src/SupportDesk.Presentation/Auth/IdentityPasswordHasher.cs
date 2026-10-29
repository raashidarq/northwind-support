using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Auth;

/// <summary>
/// ASP.NET Core Identity's password hasher (PBKDF2 with a random salt per password), which ships
/// with the framework. The hash records its own settings, so stronger settings later still
/// verify old hashes.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public string Hash(string password) => Hasher.HashPassword(User, password);

    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(User, passwordHash, password) != PasswordVerificationResult.Failed;
}
