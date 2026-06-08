namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Customers;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Asp;
using Trellis.Asp.ApiVersioning;

/// <summary>Customers controller (spec §6.1, §6.13, §7).</summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender) => _sender = sender;

    /// <summary>Create a new customer. <c>POST /api/customers</c>.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ValueTask<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken) =>
        _sender.Send(
                new CreateCustomerCommand(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.PhoneNumber,
                    request.ShippingAddress.ToDomain()),
                cancellationToken)
            .ToHttpResponseAsync(
                CustomerResponse.From,
                opts => opts
                    .CreatedAtRoute("Customers_GetById", c => new Microsoft.AspNetCore.Routing.RouteValueDictionary
                    {
                        ["id"] = c.Id.Value,
                    })
                    .WithVersionedRoute())
            .AsActionResultAsync<CustomerResponse>();

    /// <summary>
    /// Placeholder named GET (off swagger) so <see cref="Create"/>'s
    /// <c>CreatedAtRoute</c> can resolve a <c>Customers_GetById</c> route for the
    /// Location header. Spec §7 doesn't expose a "get customer by id" endpoint —
    /// this is private routing infrastructure, not API surface.
    /// </summary>
    [HttpGet("{id}", Name = "Customers_GetById")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult GetById(Guid id) => NotFound();

    /// <summary>
    /// List every order belonging to a customer. <c>GET /api/customers/{id}/orders</c>
    /// (spec §6.13). Requires <see cref="Permissions.OrdersReadAll"/>.
    /// </summary>
    [HttpGet("{id}/orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<IReadOnlyList<OrderResponse>>> ListOrders(
        CustomerId id,
        CancellationToken cancellationToken) =>
        _sender.Send(new ListOrdersByCustomerQuery(id), cancellationToken)
            .ToHttpResponseAsync(orders =>
                (IReadOnlyList<OrderResponse>)orders.Select(OrderResponse.From).ToList())
            .AsActionResultAsync<IReadOnlyList<OrderResponse>>();
}