using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Email = "alex.turner@northwind-support.example";
    private const string Password = "correct horse battery";

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _hasher.Setup(h => h.Verify("hash", Password)).Returns(true);
        _tokens.Setup(t => t.Issue(It.IsAny<CurrentAgentDto>()))
            .Returns(("signed-token", FixedClock.DefaultNow.AddHours(1)));

        _handler = new LoginCommandHandler(
            _agents.Object, _hasher.Object, _tokens.Object, NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithTheRightPassword_IssuesAToken()
    {
        ExistingAgent();

        var result = await _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None);

        Assert.Equal("signed-token", result.Token);
        Assert.Equal("Alex Turner", result.Agent.FullName);
    }

    [Fact]
    public async Task HandleAsync_WrongEmailAndWrongPassword_FailIdentically()
    {
        ExistingAgent();

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, "wrong"), CancellationToken.None));
        var wrongEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal(wrongPassword.Message, wrongEmail.Message);
        Assert.Equal(LoginCommandHandler.InvalidCredentials, wrongEmail.Message);
    }

    [Fact]
    public async Task HandleAsync_AnUnknownEmail_StillCostsAHash()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        _hasher.Verify(h => h.Hash(Password), Times.Once());
    }

    [Fact]
    public async Task HandleAsync_AnInactiveAgent_CannotSignIn()
    {
        ExistingAgent(isActive: false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        _tokens.Verify(t => t.Issue(It.IsAny<CurrentAgentDto>()), Times.Never());
    }

    private void ExistingAgent(bool isActive = true)
    {
        var agent = new Agent("Alex Turner", Email, maxOpenTickets: 10, FixedClock.DefaultNow);
        agent.SetPasswordHash("hash");

        if (!isActive)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }
}
