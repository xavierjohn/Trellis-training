namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Customers;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.ValueObjects;
using Trellis.Asp;
using Trellis.Primitives;

[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class CustomersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        var phoneNumber = request.PhoneNumber is not null
            ? Maybe.From(request.PhoneNumber)
            : Maybe.None<PhoneNumber>();

        return await ShippingAddress.TryCreate(
                request.ShippingAddress.Street,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.Country)
            .Map(address => new CreateCustomerCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                phoneNumber,
                address))
            .BindAsync(command => sender.Send(command, ct))
            .MapAsync(CustomerResponse.From)
            .ToCreatedAtActionResultAsync(this, nameof(Get), dto => new { id = dto.Id });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerResponse>> Get(
        CustomerId id,
        CancellationToken ct)
    {
        var query = new GetCustomerByIdQuery(id);

        return await sender.Send(query, ct)
            .MapAsync(CustomerResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpGet("{id}/orders")]
    public async Task<ActionResult<List<OrderResponse>>> ListOrders(
        CustomerId id,
        CancellationToken ct)
    {
        var query = new ListOrdersByCustomerQuery(id);

        return await sender.Send(query, ct)
            .MapAsync(orders => orders.Select(OrderResponse.From).ToList())
            .ToActionResultAsync(this);
    }
}
