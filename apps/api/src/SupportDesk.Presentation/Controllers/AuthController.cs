using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Presentation.Auth;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Signs an agent in. 401, with one message, for any wrong email or password.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponseDto> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>The agent the token belongs to.</summary>
    [HttpGet("me")]
    public Task<CurrentAgentDto> Me([FromServices] GetCurrentAgentQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(User.AgentId(), ct);
}
