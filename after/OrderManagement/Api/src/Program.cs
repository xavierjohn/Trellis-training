using OrderManagement.AntiCorruptionLayer;
using OrderManagement.Api;
using OrderManagement.Api.Middleware;
using OrderManagement.Application;
using Scalar.AspNetCore;
using ServiceLevelIndicators;
using Trellis.Asp;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=OrderManagement.db";

builder.Services
    .AddPresentation()
    .AddApplication()
    .AddAntiCorruptionLayer(connectionString);

var app = builder.Build();

// Ensure database is created in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
    dbContext.Database.EnsureCreated();

    app.MapOpenApi().WithDocumentPerVersion();
    app.MapScalarApiReference(
        options =>
        {
            var descriptions = app.DescribeApiVersions();

            for (var i = 0; i < descriptions.Count; i++)
            {
                var description = descriptions[i];
                var isDefault = i == descriptions.Count - 1;
                options.AddDocument(description.GroupName, description.GroupName, isDefault: isDefault);
            }
        });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseScalarValueValidation();
app.UseServiceLevelIndicator();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Main entry point for the application.
/// </summary>
public partial class Program
{
}
