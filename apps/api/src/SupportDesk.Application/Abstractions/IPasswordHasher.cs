namespace SupportDesk.Application.Abstractions;

/// <summary>
/// One-way, salted password hashing. Implemented with ASP.NET Core Identity's hasher.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
