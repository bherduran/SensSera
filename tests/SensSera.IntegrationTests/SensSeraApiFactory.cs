using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace SensSera.IntegrationTests;

/// <summary>
/// Boots the real API (full middleware, auth, EF global filters, background jobs, SignalR)
/// against a throwaway PostgreSQL container. InMemory EF can't catch provider behaviour such as
/// Npgsql's UTC-only timestamptz rule or ExecuteUpdate, so these tests run on real Postgres.
/// </summary>
public sealed class SensSeraApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18")
        .Build();

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting lands before Program.cs reads configuration (minimal hosting).
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jwt:Key", "integration-tests-only-signing-key-0123456789abcdef");
        builder.UseSetting("Jwt:Issuer", "SensSera");
        builder.UseSetting("Jwt:Audience", "SensSera");
        builder.UseSetting("Jobs:ThresholdEvaluationIntervalSeconds", "1");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SensSeraApiFactory>
{
    public const string Name = "api";
}
