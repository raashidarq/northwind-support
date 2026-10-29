namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in agent.</summary>
public sealed record CurrentAgentDto(int Id, string FullName, string Email);

/// <summary>Response of POST /api/auth/login. The token goes in an Authorization: Bearer header.</summary>
public sealed record LoginResponseDto(string Token, DateTime ExpiresAtUtc, CurrentAgentDto Agent);
