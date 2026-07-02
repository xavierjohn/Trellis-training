namespace OrderManagement.AntiCorruptionLayer;

using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>
/// Application database context with Trellis conventions.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTrellisConventions(typeof(CustomerId).Assembly);

        // SQLite cannot translate DateTimeOffset in comparisons / ORDER BY. Storing every
        // DateTimeOffset column as UTC ISO-8601 TEXT (sortable + comparable) lets server-side
        // predicates such as the overdue filter (SubmittedAt < cutoff) translate instead of
        // failing at query time. Registered as a model-wide convention so it also reaches the
        // Maybe<DateTimeOffset> backing columns (SubmittedAt/ShippedAt/PaidAt) that only
        // materialize after conventions run. Instant-preserving; values read back as UTC.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcIso8601DateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.AddTrellisOutbox();
        modelBuilder.AddTrellisInbox();
    }

    private sealed class UtcIso8601DateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
    {
        public UtcIso8601DateTimeOffsetConverter()
            : base(
                v => v.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                v => DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
        {
        }
    }
}
