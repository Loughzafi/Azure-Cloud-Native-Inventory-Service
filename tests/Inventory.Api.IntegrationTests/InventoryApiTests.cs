using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Inventory.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Inventory.Api.IntegrationTests;

public sealed class ApiFixture : IAsyncLifetime
{
    private const string Issuer = "https://issuer.inventory.test";
    private const string Audience = "inventory-test";
    private readonly RSA rsa = RSA.Create(2048);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();
    private WebApplicationFactory<InventoryItem>? factory;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "schema.sql")), connection);
        await command.ExecuteNonQueryAsync();

        var oidc = new OpenIdConnectConfiguration { Issuer = Issuer };
        oidc.SigningKeys.Add(new RsaSecurityKey(rsa));
        // Minimal hosting reads configuration before WebApplicationFactory's callbacks run.
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings__Postgres"] = database.GetConnectionString(),
            ["Authentication__Authority"] = Issuer,
            ["Authentication__Audience"] = Audience,
            ["Authentication__RequiredScope"] = "inventory.access",
            ["Authentication__RequiredRole"] = "Inventory.ReadWrite"
        };
        var previous = settings.ToDictionary(x => x.Key, x => Environment.GetEnvironmentVariable(x.Key));
        try
        {
            foreach (var setting in settings) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            factory = new WebApplicationFactory<InventoryItem>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc);
                        options.TokenValidationParameters.ValidIssuer = Issuer;
                        options.TokenValidationParameters.ValidAudience = Audience;
                    }));
            });
            using var startupClient = factory.CreateClient();
        }
        finally
        {
            foreach (var setting in previous) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }

    public HttpClient Client(string? token = null)
    {
        var client = factory!.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public string Token(string? scope = null, string? role = null, string audience = Audience,
        string issuer = Issuer, bool expired = false)
    {
        var claims = new List<Claim>();
        if (scope is not null) claims.Add(new Claim("scp", scope));
        if (role is not null) claims.Add(new Claim("roles", role));
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, audience, claims,
            now.AddHours(-2), expired ? now.AddHours(-1) : now.AddMinutes(10),
            new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        await database.DisposeAsync();
        rsa.Dispose();
    }
}

public sealed class InventoryApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Path = "/api/inventory";

    [Fact]
    public async Task AuthenticationAndAuthorizationAreEnforced()
    {
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path)).StatusCode);
        using var malformed = fixture.Client("not-a-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await malformed.GetAsync(Path)).StatusCode);
        using var wrongAudience = fixture.Client(fixture.Token(scope: "inventory.access", audience: "other"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongAudience.GetAsync(Path)).StatusCode);
        using var wrongIssuer = fixture.Client(fixture.Token(scope: "inventory.access", issuer: "https://other.test"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongIssuer.GetAsync(Path)).StatusCode);
        using var expired = fixture.Client(fixture.Token(scope: "inventory.access", expired: true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync(Path)).StatusCode);
        using var noPermission = fixture.Client(fixture.Token());
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync(Path)).StatusCode);
        using var role = fixture.Client(fixture.Token(role: "Inventory.ReadWrite"));
        Assert.Equal(HttpStatusCode.OK, (await role.GetAsync(Path)).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCrudValidationAndConflicts()
    {
        using var client = fixture.Client(fixture.Token(scope: "other inventory.access"));
        var sku = $"test-{Guid.NewGuid():N}";
        var otherSku = $"test-{Guid.NewGuid():N}";
        var missing = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Path}/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Path}/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"{Path}/{missing}", new InventoryItemRequest(otherSku, "Missing", 1))).StatusCode);

        var invalid = await client.PostAsJsonAsync(Path, new InventoryItemRequest(" ", null, -1));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var create = await client.PostAsJsonAsync(Path, new InventoryItemRequest($" {sku} ", " First ", 2));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var item = (await create.Content.ReadFromJsonAsync<InventoryItem>())!;
        Assert.Equal(sku, item.Sku);
        Assert.Equal("First", item.Name);
        Assert.Equal($"/api/inventory/{item.Id}", create.Headers.Location!.ToString());

        var list = await client.GetFromJsonAsync<InventoryItem[]>(Path);
        Assert.Contains(list!, x => x.Id == item.Id);
        var read = await client.GetFromJsonAsync<InventoryItem>($"{Path}/{item.Id}");
        Assert.Equal(item, read);

        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(Path, new InventoryItemRequest(sku.ToUpperInvariant(), "Duplicate", 1))).StatusCode);
        var second = await client.PostAsJsonAsync(Path, new InventoryItemRequest(otherSku, "Second", 1));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondItem = (await second.Content.ReadFromJsonAsync<InventoryItem>())!;
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PutAsJsonAsync($"{Path}/{secondItem.Id}", new InventoryItemRequest(sku.ToUpperInvariant(), "Duplicate", 3))).StatusCode);
        Assert.Equal(otherSku, (await client.GetFromJsonAsync<InventoryItem>($"{Path}/{secondItem.Id}"))!.Sku);

        var update = await client.PutAsJsonAsync($"{Path}/{item.Id}", new InventoryItemRequest(sku, "Updated", 7));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<InventoryItem>())!;
        Assert.Equal("Updated", updated.Name);
        Assert.Equal(7, updated.Quantity);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Path}/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Path}/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Path}/{secondItem.Id}")).StatusCode);
    }
}
