using System.Text.Json;
using Vanilla;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Db") ?? "Data Source=vanilla-arm.db"));

var app = builder.Build();

// Fresh schema on startup so each benchmark run starts from an empty database.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok("ok"));

// ---- Products -------------------------------------------------------------

app.MapPost("/products", async (CreateProductRequest req, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || req.Stock < 0 || req.Price < 0)
        return Results.UnprocessableEntity(new { detail = "name must be non-empty; stock and price must be >= 0." });

    var product = new Product { Id = Guid.NewGuid(), Name = req.Name, Stock = req.Stock, Price = req.Price };
    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/products/{product.Id}",
        new { product.Id, product.Name, product.Stock, product.Price });
});

app.MapGet("/products/{id:guid}", async (Guid id, AppDb db) =>
{
    var p = await db.Products.FindAsync(id);
    return p is null
        ? Results.NotFound()
        : Results.Ok(new { p.Id, p.Name, p.Stock, p.Price });
});

// ---- Orders ---------------------------------------------------------------

app.MapPost("/orders", async (CreateOrderRequest req, AppDb db) =>
{
    if (req.Items is null || req.Items.Length == 0)
        return Results.UnprocessableEntity(new { detail = "an order must have at least one item." });

    if (req.Items.Any(i => i.Quantity < 1))
        return Results.UnprocessableEntity(new { detail = "every item quantity must be >= 1." });

    foreach (var item in req.Items)
    {
        if (await db.Products.FindAsync(item.ProductId) is null)
            return Results.UnprocessableEntity(new { detail = $"product {item.ProductId} does not exist." });
    }

    var order = new Order
    {
        Id = Guid.NewGuid(),
        CustomerId = req.CustomerId,
        Status = "Draft",
        Items = req.Items
            .Select(i => new LineItem { Id = Guid.NewGuid(), ProductId = i.ProductId, Quantity = i.Quantity })
            .ToList(),
    };
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    return Results.Created($"/orders/{order.Id}", new
    {
        order.Id,
        order.Status,
        order.CustomerId,
        items = order.Items.Select(i => new { i.ProductId, i.Quantity }),
    });
});

app.MapGet("/orders/{id:guid}", async (Guid id, AppDb db) =>
{
    var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
    return order is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            order.Id,
            order.Status,
            order.SubmittedAt,
            order.CustomerId,
            items = order.Items.Select(i => new { i.ProductId, i.Quantity }),
        });
});

// ---- Submit (the operation under test) ------------------------------------

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
        return Results.NotFound(new { detail = ex.Message });
    }
    catch (Exception ex)
    {
        // BUG 3 (R3): every business failure (insufficient stock, invalid state) collapses to 500 and
        // leaks the internal exception message. The caller cannot tell a real server fault from a
        // rejected business rule.
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();

static string[] ParseActorPermissions(HttpContext ctx)
{
    var header = ctx.Request.Headers["X-Actor"].ToString();
    if (string.IsNullOrEmpty(header))
        return [];

    try
    {
        var actor = JsonSerializer.Deserialize<ActorDto>(header,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return actor?.Permissions ?? [];
    }
    catch
    {
        return [];
    }
}

internal sealed record CreateProductRequest(string Name, int Stock, decimal Price);
internal sealed record CreateOrderRequest(Guid CustomerId, OrderItemRequest[] Items);
internal sealed record OrderItemRequest(Guid ProductId, int Quantity);
internal sealed record ActorDto(string Id, string[] Permissions);

// Exposed so a test host could drive the real HTTP pipeline via WebApplicationFactory.
public partial class Program;
