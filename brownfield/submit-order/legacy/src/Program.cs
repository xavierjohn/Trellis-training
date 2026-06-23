using System.Text.Json;
using Legacy;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Db") ?? "Data Source=legacy-orders.db"));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/orders/{id:guid}/submit", async (Guid id, HttpContext ctx, AppDb db) =>
{
    var permissions = ParseActorPermissions(ctx);
    try
    {
        var order = await SubmitOrderEndpoint.SubmitAsync(db, id, permissions);
        return Results.Ok(new { order.Id, order.Status, order.SubmittedAt });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        // BUG 3: every business failure (insufficient stock, invalid state) collapses to 500 and
        // leaks the internal exception message. The caller cannot tell a real server fault from a
        // rejected business rule, and the message exposes internal detail.
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();

static string[] ParseActorPermissions(HttpContext ctx)
{
    var header = ctx.Request.Headers["X-Test-Actor"].ToString();
    if (string.IsNullOrEmpty(header))
        return ["*"]; // default "god-mode" actor — another footgun, but not the point of this lab

    try
    {
        var actor = JsonSerializer.Deserialize<ActorDto>(header);
        return actor?.Permissions ?? [];
    }
    catch
    {
        return [];
    }
}

internal sealed record ActorDto(string Id, string[] Permissions);

// Exposed so the integration tests can drive the real HTTP pipeline via WebApplicationFactory.
public partial class Program;
