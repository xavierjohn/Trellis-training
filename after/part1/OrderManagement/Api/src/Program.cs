using OrderManagement.AntiCorruptionLayer;
using OrderManagement.Api;
using OrderManagement.Api.Middleware;
using OrderManagement.Application;
using Scalar.AspNetCore;
using ServiceLevelIndicators;
using Trellis.Asp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddPresentation()
    .AddApplication()
    .AddAntiCorruptionLayer(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrderManagement.AntiCorruptionLayer.AppDbContext>();
    db.Database.EnsureCreated();

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
