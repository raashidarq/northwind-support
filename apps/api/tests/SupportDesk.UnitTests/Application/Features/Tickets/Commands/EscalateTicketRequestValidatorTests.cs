using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    [Theory]
    [InlineData("Lost", false)]
    [InlineData("   Lost   ", false)]
    [InlineData("Late!", true)]
    public void Reason_MustBeAtLeastFiveCharactersOnceTrimmed(string reason, bool valid) =>
        Assert.Equal(valid, _validator.Validate(new EscalateTicketRequest(reason, "Team lead")).IsValid);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Reason_MustBeAtMostFiveHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, _validator.Validate(new EscalateTicketRequest(new string('a', length), "Team lead")).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EscalatedBy_IsRequired(string escalatedBy) =>
        Assert.False(_validator.Validate(new EscalateTicketRequest("Customer is losing sales", escalatedBy)).IsValid);
}
