namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Commands;
using OrderManagement.Domain;
using Trellis.Asp;
using Trellis.Primitives;

/// <summary>
/// Products management controller.
/// </summary>
[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class ProductsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Create a new product.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var productNameResult = ProductName.TryCreate(request.ProductName);
        var skuResult = Sku.TryCreate(request.Sku);
        var priceResult = Money.TryCreate(request.UnitPriceAmount, request.UnitPriceCurrency);

        var combined = productNameResult.Combine(skuResult).Combine(priceResult);
        if (!combined.TryGetValue(out var values))
            return combined.Error.ToActionResult<ProductDto>(this);

        var (productName, sku, price) = values;
        var command = new CreateProductCommand(productName, sku, price);

        return await sender.Send(command, cancellationToken)
            .MapAsync(MapToDto)
            .ToCreatedAtActionResultAsync(this, nameof(GetProduct), dto => new { id = dto.Id });
    }

    /// <summary>
    /// Get a product by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetProduct(Guid id) =>
        NotFound(new { error = "Use GET /api/Products/{id} via query when needed" });

    /// <summary>
    /// Add stock to a product.
    /// </summary>
    [HttpPost("{id}/stock")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> AddStock(
        Guid id,
        [FromBody] AddStockRequest request,
        CancellationToken cancellationToken)
    {
        return await ProductId.TryCreate(id)
            .Map(productId => new AddStockCommand(productId, request.Quantity))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);
    }

    private static ProductDto MapToDto(Product p) => new(
        p.Id.Value,
        p.ProductName.Value,
        p.Sku.Value,
        p.UnitPrice.Amount,
        p.UnitPrice.Currency.Value,
        p.StockQuantity);
}
