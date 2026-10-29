using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application;
using SupportDesk.Application.Abstractions;
using SupportDesk.Presentation.Auth;
using SupportDesk.Presentation.Health;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The SLA numbers live in configuration (BR-6); without them no due date can be worked out.
builder.Services.AddSingleton(builder.Configuration.GetSection("Sla").Get<SlaPolicy>()
    ?? throw new InvalidOperationException("The 'Sla' configuration section is missing."));

// Sign-in. The signing key only ever comes from configuration (appsettings.Development.json for a
// developer machine, the Jwt__Key environment variable anywhere else); the API refuses to start
// without a long enough one.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

if (jwt.Key.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be set to at least 32 characters. See .env.example.");
}

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as they are in the token ("sub", "name"), not .NET's long URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = JwtTokenIssuer.SigningKey(jwt),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Every endpoint needs a signed-in agent unless it says [AllowAnonymous] (only login does), so a
// new endpoint is protected without anyone remembering to protect it.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Keeps a developer machine one command away from a working database.
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// For Docker and load balancers: 200 "Healthy" when the database answers, 503 when it does not.
// Open without a token, so it is the one other exception to the fallback policy above.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
