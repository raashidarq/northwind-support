using FluentValidation;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();

        RuleFor(x => x.Password).NotEmpty();
    }
}
