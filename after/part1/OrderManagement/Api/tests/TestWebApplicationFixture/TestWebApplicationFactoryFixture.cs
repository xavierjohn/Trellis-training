namespace Api.Tests;

using System;
using Application.Tests;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OrderManagement.AntiCorruptionLayer;
using Trellis.EntityFrameworkCore;
using Xunit.Sdk;
using Xunit.v3;

public class TestWebApplicationFactoryFixture : WebApplicationFactory<Program>, ITestOutputHelperAccessor
{
    private SqliteConnection? _connection;

    public TestWebApplicationFactoryFixture()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    public ITestOutputHelper? OutputHelper { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging((p) => p.AddXUnit(this));

        builder.ConfigureServices(services =>
        {
            // Replace EF DbContext with SQLite in-memory
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection)
                       .AddInterceptors(new MaybeQueryInterceptor()));

            // Ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}

[CollectionDefinition(Id)]
public class TestWebApplicationFactoryCollectionFixture : ICollectionFixture<TestWebApplicationFactoryFixture>
{
    public const string Id = "Test web application factory fixture collection";
}
