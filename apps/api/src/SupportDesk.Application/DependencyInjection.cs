using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Features.Agents.Queries.GetAgents;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Application.Features.Categories.Queries.GetCategories;
using SupportDesk.Application.Features.Customers.Queries.GetCustomer;
using SupportDesk.Application.Features.Customers.Queries.GetCustomers;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetEscalations;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;

namespace SupportDesk.Application;

/// <summary>
/// Registers the command and query handlers and the request validators.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RaiseTicketCommandHandler>();
        services.AddScoped<ChangeTicketStatusCommandHandler>();
        services.AddScoped<AssignTicketCommandHandler>();
        services.AddScoped<EscalateTicketCommandHandler>();
        services.AddScoped<SearchTicketsQueryHandler>();
        services.AddScoped<GetTicketQueryHandler>();
        services.AddScoped<GetEscalationsQueryHandler>();

        services.AddScoped<GetCustomersQueryHandler>();
        services.AddScoped<GetCustomerQueryHandler>();

        services.AddScoped<GetAgentsQueryHandler>();

        services.AddScoped<GetCategoriesQueryHandler>();

        services.AddScoped<GetSlaSummaryQueryHandler>();

        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<GetCurrentAgentQueryHandler>();

        services.AddValidatorsFromAssemblyContaining<RaiseTicketCommandHandler>(ServiceLifetime.Singleton);

        return services;
    }
}
