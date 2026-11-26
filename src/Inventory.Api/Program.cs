using Inventory.Api.Data;
using Inventory.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Postgres.");
var authority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Configure Authentication:Authority.");
var audience = builder.Configuration["Authentication:Audience"]
    ?? throw new InvalidOperationException("Configure Authentication:Audience.");
if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityEndpoint)
    || authorityEndpoint.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException("Authentication:Authority must be an HTTPS URL.");
}

if (string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("Authentication:Audience cannot be empty.");
}
var requiredScope = builder.Configuration["Authentication:RequiredScope"] ?? "inventory.access";
var requiredRole = builder.Configuration["Authentication:RequiredRole"] ?? "Inventory.ReadWrite";

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authorityEndpoint.ToString();
        options.Audience = audience;
        options.MapInboundClaims = false;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("InventoryAccess", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context =>
            context.User.FindAll("scp")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains(requiredScope, StringComparer.Ordinal)
            || context.User.HasClaim("roles", requiredRole)));
});
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres");

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
}).AllowAnonymous();

app.MapInventoryEndpoints().RequireAuthorization("InventoryAccess");

app.Run();
