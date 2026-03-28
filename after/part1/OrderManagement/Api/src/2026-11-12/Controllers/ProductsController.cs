namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using ServiceLevelIndicators;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Asp;

/// <summary>
/// Products controller.
/// </summary>
[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>Constructor.</summary>
    public ProductsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Create a new product.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async ValueTask<ActionResult<ProductResponse>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken) =>
        await _sender.Send(
            new CreateProductCommand(request.ProductName, request.Sku, request.UnitPrice),
            cancellationToken)
            .ToCreatedAtActionResultAsync(this, nameof(GetProduct), p => new { id = (Guid)p.Id }, ProductResponse.From);

    /// <summary>
    /// Get a product by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<ProductResponse>> GetProduct(
        [CustomerResourceId] ProductId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new GetProductByIdQuery(id), cancellationToken)
            .ToActionResultAsync(this, ProductResponse.From);

    /// <summary>
    /// Add stock to a product.
    /// </summary>
    [HttpPost("{id}/stock-additions")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<ProductResponse>> AddStock(
        [CustomerResourceId] ProductId id,
        [FromBody] AddStockRequest request,
        CancellationToken cancellationToken) =>
        await _sender.Send(new AddStockCommand(id, request.Quantity), cancellationToken)
            .ToActionResultAsync(this, ProductResponse.From);
}
