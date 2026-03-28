namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using ServiceLevelIndicators;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Customers;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Asp;

/// <summary>
/// Customers controller.
/// </summary>
[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>Constructor.</summary>
    public CustomersController(ISender sender) => _sender = sender;

    /// <summary>
    /// Create a new customer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async ValueTask<ActionResult<CustomerResponse>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var addressResult = ShippingAddress.TryCreate(
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.Country);

        return await addressResult
            .BindAsync(address => _sender.Send(
                new CreateCustomerCommand(request.FirstName, request.LastName, request.Email, request.PhoneNumber, address),
                cancellationToken))
            .ToCreatedAtActionResultAsync(this, nameof(GetCustomer), c => new { id = (Guid)c.Id }, CustomerResponse.From);
    }

    /// <summary>
    /// Get a customer by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<CustomerResponse>> GetCustomer(
        [CustomerResourceId] CustomerId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new GetCustomerByIdQuery(id), cancellationToken)
            .ToActionResultAsync(this, CustomerResponse.From);

    /// <summary>
    /// List orders for a customer.
    /// </summary>
    [HttpGet("{id}/orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<IReadOnlyList<OrderResponse>>> GetCustomerOrders(
        [CustomerResourceId] CustomerId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new ListOrdersByCustomerQuery(id), cancellationToken)
            .ToActionResultAsync(this, orders => (IReadOnlyList<OrderResponse>)orders.Select(OrderResponse.From).ToList());
}
