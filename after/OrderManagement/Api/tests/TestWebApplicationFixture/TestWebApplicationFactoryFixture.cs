namespace Api.Tests;

using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderManagement.AntiCorruptionLayer;
using Trellis.EntityFrameworkCore;
using Trellis.Testing;
using Trellis.Testing.AspNetCore;
using Xunit.v3;

public class TestWebApplicationFactoryFixture : WebApplicationFactory<Program>, ITestOutputHelperAccessor
{
    private readonly SqliteConnection? _connection;
    private static bool UseRealServices =>
        string.Equals(Environment.GetEnvironmentVariable("USE_REAL_SERVICES"), "true", StringComparison.OrdinalIgnoreCase);

    public TestWebApplicationFactoryFixture()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        if (!UseRealServices)
        {
            // Keep a persistent connection for in-memory SQLite
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }
    }

    public ITestOutputHelper? OutputHelper { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(p => p.AddXUnit(this));

        // Disable the background eventing services (outbox relay, broker consumer, and the
        // development payment simulator) so integration tests drive the outbox/inbox payment
        // round-trip deterministically through IInboxDispatcher instead of racing asynchronous
        // background delivery.
        builder.ConfigureServices(services => services.RemoveAll<IHostedService>());

        if (UseRealServices)
            return;

        builder.ConfigureServices(services =>
            services.ReplaceDbProvider<AppDbContext>(options =>
                options.UseSqlite(_connection!)
                       .AddTrellisInterceptors()
                       .AddTrellisOutboxInterceptor()));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection?.Dispose();
    }
}

[CollectionDefinition(Id)]
public class TestWebApplicationFactoryCollectionFixture : ICollectionFixture<TestWebApplicationFactoryFixture>
{
    public const string Id = "Test web application factory fixture collection";
}
