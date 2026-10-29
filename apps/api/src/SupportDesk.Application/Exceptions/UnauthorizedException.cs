namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when the caller cannot be identified, e.g. a failed login. Surfaces as HTTP 401.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
