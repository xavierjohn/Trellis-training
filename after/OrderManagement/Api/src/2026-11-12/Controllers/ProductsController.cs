namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Products;
using OrderManagement.Domain.ValueObjects;
using Trellis.Asp;
using Trellis.Primitives;

[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class ProductsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken ct) =>
        await Money.TryCreate(request.UnitPrice, "USD")
            .Map(unitPrice => new CreateProductCommand(
                request.ProductName,
                request.Sku,
                unitPrice))
            .BindAsync(command => sender.Send(command, ct))
            .MapAsync(ProductResponse.From)
            .ToCreatedAtActionResultAsync(this, nameof(Get), dto => new { id = dto.Id });

    [HttpPost("{id}/stock-additions")]
    public async Task<ActionResult<ProductResponse>> AddStock(
        ProductId id,
        [FromBody] AddStockRequest request,
        CancellationToken ct)
    {
        var command = new AddStockCommand(id, request.Quantity);

        return await sender.Send(command, ct)
            .MapAsync(ProductResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponse>> Get(
        ProductId id,
        CancellationToken ct)
    {
        var query = new GetProductByIdQuery(id);

        return await sender.Send(query, ct)
            .MapAsync(ProductResponse.From)
            .ToActionResultAsync(this);
    }
}
