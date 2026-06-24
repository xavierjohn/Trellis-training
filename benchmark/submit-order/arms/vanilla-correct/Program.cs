using System.Text.Json;
using VanillaCorrect;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Db") ?? "Data Source=vanilla-correct-arm.db"));

builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok("ok"));

// ---- Products -------------------------------------------------------------

app.MapPost("/products", async (CreateProductRequest req, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || req.Stock < 0 || req.Price < 0)
        return Results.Problem(detail: "name must be non-empty; stock and price must be >= 0.", statusCode: 422);

    var product = new Product
    {
        Id = Guid.NewGuid(),
        Name = req.Name,
        Stock = req.Stock,
        Price = req.Price,
        Version = Guid.NewGuid(),
    };
    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/products/{product.Id}",
        new { product.Id, product.Name, product.Stock, product.Price });
});

app.MapGet("/products/{id:guid}", async (Guid id, AppDb db) =>
{
    var p = await db.Products.FindAsync(id);
    return p is null ? Results.NotFound() : Results.Ok(new { p.Id, p.Name, p.Stock, p.Price });
});

// ---- Orders ---------------------------------------------------------------

app.MapPost("/orders", async (CreateOrderRequest req, AppDb db) =>
{
    if (req.Items is null || req.Items.Length == 0)
        return Results.Problem(detail: "an order must have at least one item.", statusCode: 422);

    if (req.Items.Any(i => i.Quantity < 1))
        return Results.Problem(detail: "every item quantity must be >= 1.", statusCode: 422);

    foreach (var item in req.Items)
    {
        if (await db.Products.FindAsync(item.ProductId) is null)
            return Results.Problem(detail: $"product {item.ProductId} does not exist.", statusCode: 422);
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
    // R5: authorization, by hand.
    if (!HasPermission(ctx, "orders:submit"))
        return Results.Problem(detail: "Caller lacks the 'orders:submit' permission.", statusCode: 403);

    var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
    if (order is null)
        return Results.Problem(detail: $"Order {id} not found.", statusCode: 404);

    // R4: state guard, by hand.
    if (order.Status != "Draft")
        return Results.Problem(detail: $"Order is {order.Status}; only a Draft order can be submitted.", statusCode: 409);

    if (order.Items.Count == 0)
        return Results.Problem(detail: "Cannot submit an order without line items.", statusCode: 422);

    // Aggregate demand per product so duplicate lines on the same product are checked together.
    var demand = order.Items
        .GroupBy(i => i.ProductId)
        .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
        .ToList();

    var products = new Dictionary<Guid, Product>();
    foreach (var d in demand)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == d.ProductId);
        if (product is null)
            return Results.Problem(detail: $"Product {d.ProductId} not found.", statusCode: 404);
        products[d.ProductId] = product;
    }

    // R2 phase 1: validate every reservation before mutating anything. R3: a shortfall is a 422
    // value, not a thrown exception that would become a 500.
    foreach (var d in demand)
    {
        var product = products[d.ProductId];
        if (product.Stock < d.Quantity)
            return Results.Problem(
                detail: $"Product '{product.Name}' has insufficient stock: requested {d.Quantity}, available {product.Stock}.",
                statusCode: 422);
    }

    // R2 phase 2: apply, bumping each product's concurrency token. R1: a concurrent submit that
    // reserved the same stock first changed the token, so this commit's WHERE Version=@original
    // matches no row and SaveChanges throws — caught below.
    foreach (var d in demand)
    {
        var product = products[d.ProductId];
        product.Stock -= d.Quantity;
        product.Version = Guid.NewGuid();
    }
    order.Status = "Submitted";
    order.SubmittedAt = DateTime.UtcNow;

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Problem(detail: "The order could not be submitted due to a concurrent update. Please retry.", statusCode: 409);
    }

    return Results.Ok(new { id = order.Id, status = order.Status, submittedAt = order.SubmittedAt });
});

app.Run();

static bool HasPermission(HttpContext ctx, string permission)
{
    var header = ctx.Request.Headers["X-Actor"].ToString();
    if (string.IsNullOrEmpty(header))
        return false;

    try
    {
        var actor = JsonSerializer.Deserialize<ActorDto>(header,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return actor?.Permissions?.Contains(permission, StringComparer.Ordinal) ?? false;
    }
    catch (JsonException)
    {
        return false;
    }
}

internal sealed record CreateProductRequest(string Name, int Stock, decimal Price);
internal sealed record CreateOrderRequest(Guid CustomerId, OrderItemRequest[] Items);
internal sealed record OrderItemRequest(Guid ProductId, int Quantity);
internal sealed record ActorDto(string? Id, string[]? Permissions);

public partial class Program;
