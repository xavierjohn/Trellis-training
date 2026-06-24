using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trellis.EntityFrameworkCore;
using TrellisArm;
using IResult = Microsoft.AspNetCore.Http.IResult;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Db") ?? "Data Source=trellis-arm.db")
           .AddTrellisInterceptors());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();

var app = builder.Build();

// Fresh schema on startup so each benchmark run starts from an empty database.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

// Renders any unhandled exception as an RFC 9457 ProblemDetails (no leaked internals).
app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok("ok"));

// ---- Products -------------------------------------------------------------

app.MapPost("/products", async (CreateProductRequest req, AppDb db) =>
{
    var result = Product.Create(req.Name, req.Stock, req.Price);
    if (!result.TryGetValue(out var product))
        return ToProblem(result.Error!);

    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id.Value}", ProductView(product));
});

app.MapGet("/products/{id:guid}", async (Guid id, AppDb db) =>
{
    if (!ProductId.TryCreate(id).TryGetValue(out var productId))
        return Results.NotFound();

    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
    return product is null ? Results.NotFound() : Results.Ok(ProductView(product));
});

// ---- Orders ---------------------------------------------------------------

app.MapPost("/orders", async (CreateOrderRequest req, AppDb db) =>
{
    if (!CustomerId.TryCreate(req.CustomerId).TryGetValue(out var customerId))
        return ToProblem(Error.InvalidInput.ForField("customerId", "order.customer.invalid", "customerId is invalid."));

    var items = new List<(ProductId ProductId, int Quantity)>();
    foreach (var item in req.Items ?? [])
    {
        if (!ProductId.TryCreate(item.ProductId).TryGetValue(out var productId))
            return ToProblem(Error.InvalidInput.ForField("productId", "order.product.invalid", "A line item productId is invalid."));
        items.Add((productId, item.Quantity));
    }

    var orderResult = Order.CreateDraft(customerId, items);
    if (!orderResult.TryGetValue(out var order))
        return ToProblem(orderResult.Error!);

    // Every referenced product must exist.
    foreach (var productId in items.Select(i => i.ProductId).Distinct())
    {
        if (await db.Products.FirstOrDefaultAsync(p => p.Id == productId) is null)
            return ToProblem(
                Error.InvalidInput.ForRule("order.unknown-product", $"Product {productId.Value} does not exist."));
    }

    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id.Value}", OrderView(order));
});

app.MapGet("/orders/{id:guid}", async (Guid id, AppDb db) =>
{
    if (!OrderId.TryCreate(id).TryGetValue(out var orderId))
        return Results.NotFound();

    var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
    return order is null ? Results.NotFound() : Results.Ok(OrderView(order));
});

// ---- Submit (the operation under test) ------------------------------------

app.MapPost("/orders/{id:guid}/submit", async (Guid id, HttpContext ctx, AppDb db, TimeProvider clock) =>
{
    // R5: only a caller holding orders:submit may submit.
    if (!HasPermission(ctx, "orders:submit"))
        return ToProblem(new Error.Forbidden("orders:submit") { Detail = "Caller lacks the 'orders:submit' permission." });

    if (!OrderId.TryCreate(id).TryGetValue(out var orderId))
        return ToProblem(new Error.NotFound(ResourceRef.For<Order>(id)) { Detail = $"Order {id} not found." });

    // Owned line items are loaded with the order automatically.
    var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
    if (order is null)
        return ToProblem(new Error.NotFound(ResourceRef.For<Order>(id)) { Detail = $"Order {id} not found." });

    var products = new Dictionary<ProductId, Product>();
    foreach (var productId in order.LineItems.Select(li => li.ProductId).Distinct())
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product is not null)
            products[productId] = product;
    }

    var result = order.Submit(products, clock);
    if (result.IsFailure)
        return ToProblem(result.Error!);

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        // R1: a concurrent submit reserved the same stock first and bumped the product's ETag, so
        // this commit's optimistic-concurrency check failed. The loser is rejected (and may retry)
        // instead of overselling.
        return ToProblem(
            new Error.Conflict(ResourceRef.For<Order>(id), "order.concurrent-submit")
            {
                Detail = "The order could not be submitted due to a concurrent update. Please retry.",
            });
    }

    return Results.Ok(new { id = order.Id.Value, status = order.Status.Value, submittedAt = order.SubmittedAt });
});

app.Run();

// ---- Helpers --------------------------------------------------------------

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

static IResult ToProblem(Error error)
{
    var status = error switch
    {
        Error.NotFound => StatusCodes.Status404NotFound,
        Error.Conflict => StatusCodes.Status409Conflict,
        Error.Forbidden => StatusCodes.Status403Forbidden,
        Error.InvalidInput => StatusCodes.Status422UnprocessableEntity,
        Error.InvariantViolation => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };
    return Results.Problem(detail: error.GetDisplayMessage(), statusCode: status, title: error.Code);
}

static object ProductView(Product p) =>
    new { id = p.Id.Value, name = p.Name, stock = p.Stock.Value, price = p.Price };

static object OrderView(Order o) =>
    new
    {
        id = o.Id.Value,
        status = o.Status.Value,
        submittedAt = o.SubmittedAt,
        customerId = o.CustomerId.Value,
        items = o.LineItems.Select(li => new { productId = li.ProductId.Value, quantity = li.Quantity.Value }),
    };

internal sealed record CreateProductRequest(string Name, int Stock, decimal Price);
internal sealed record CreateOrderRequest(Guid CustomerId, OrderItemRequest[] Items);
internal sealed record OrderItemRequest(Guid ProductId, int Quantity);
internal sealed record ActorDto(string? Id, string[]? Permissions);

// Exposed so a test host could drive the real HTTP pipeline via WebApplicationFactory.
public partial class Program;
