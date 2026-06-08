namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Asp;
using Trellis.Asp.ApiVersioning;

/// <summary>Products controller (spec §6.2, §6.3, §7).</summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductsController(ISender sender) => _sender = sender;

    /// <summary>Create a new product. <c>POST /api/products</c>.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ValueTask<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken) =>
        _sender.Send(new CreateProductCommand(request.ProductName, request.Sku, request.UnitPrice), cancellationToken)
            .ToHttpResponseAsync(
                ProductResponse.From,
                opts => opts
                    .CreatedAtRoute("Products_GetById", p => new Microsoft.AspNetCore.Routing.RouteValueDictionary
                    {
                        ["id"] = p.Id.Value,
                    })
                    .WithVersionedRoute())
            .AsActionResultAsync<ProductResponse>();

    /// <summary>
    /// Placeholder named GET (off swagger) so <see cref="Create"/>'s <c>CreatedAtRoute</c>
    /// can resolve a <c>Products_GetById</c> route for the Location header.
    /// </summary>
    [HttpGet("{id}", Name = "Products_GetById")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult GetById(Guid id) => NotFound();

    /// <summary>Add stock to a product. <c>POST /api/products/{id}/stock-additions</c>.</summary>
    [HttpPost("{id}/stock-additions")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<ProductResponse>> AddStock(
        ProductId id,
        [FromBody] AddStockRequest request,
        CancellationToken cancellationToken) =>
        _sender.Send(new AddStockCommand(id, request.Quantity), cancellationToken)
            .ToHttpResponseAsync(ProductResponse.From)
            .AsActionResultAsync<ProductResponse>();
}
