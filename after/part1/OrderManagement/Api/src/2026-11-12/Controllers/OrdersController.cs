namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Commands;
using OrderManagement.Application.Queries;
using OrderManagement.Domain;
using Trellis.Asp;

/// <summary>
/// Orders management controller.
/// </summary>
[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class OrdersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Create a draft order.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        [FromBody] CreateDraftOrderRequest request,
        CancellationToken cancellationToken)
    {
        return await CustomerId.TryCreate(request.CustomerId)
            .Map(customerId => new CreateDraftOrderCommand(
                customerId,
                request.LineItems.Select(li =>
                    new Application.Commands.LineItemRequest(ProductId.Create(li.ProductId), li.Quantity)).ToList()))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToCreatedAtActionResultAsync(this, nameof(GetOrder), dto => new { id = dto.Id });
    }

    /// <summary>
    /// Get an order by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(string id, CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Map(orderId => new GetOrderByIdQuery(orderId))
            .BindAsync(query => sender.Send(query, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// Add a line item to an order.
    /// </summary>
    [HttpPost("{id}/line-items")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> AddLineItem(
        string id,
        [FromBody] AddLineItemRequest request,
        CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Combine(ProductId.TryCreate(request.ProductId))
            .Map((orderId, productId) => new AddLineItemCommand(orderId, productId, request.Quantity))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// Remove a line item from an order.
    /// </summary>
    [HttpDelete("{id}/line-items/{lineItemId}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> RemoveLineItem(
        string id,
        string lineItemId,
        CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Combine(LineItemId.TryCreate(lineItemId))
            .Map((orderId, liId) => new RemoveLineItemCommand(orderId, liId))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// Submit an order.
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> SubmitOrder(string id, CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Map(orderId => new SubmitOrderCommand(orderId))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// Approve an order.
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> ApproveOrder(string id, CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Map(orderId => new ApproveOrderCommand(orderId))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// Ship an order.
    /// </summary>
    [HttpPost("{id}/ship")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> ShipOrder(string id, CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Map(orderId => new ShipOrderCommand(orderId))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// Deliver an order.
    /// </summary>
    [HttpPost("{id}/deliver")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> DeliverOrder(string id, CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Map(orderId => new DeliverOrderCommand(orderId))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// Cancel an order.
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderDto>> CancelOrder(string id, CancellationToken cancellationToken) =>
        await OrderId.TryCreate(id)
            .Map(orderId => new CancelOrderCommand(orderId))
            .BindAsync(command => sender.Send(command, cancellationToken))
            .MapAsync(MapToDto)
            .ToActionResultAsync(this);

    /// <summary>
    /// List orders for a customer.
    /// </summary>
    [HttpGet("by-customer/{customerId}")]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrderDto>>> ListOrdersByCustomer(
        Guid customerId,
        CancellationToken cancellationToken) =>
        await CustomerId.TryCreate(customerId)
            .Map(id => new ListOrdersByCustomerQuery(id))
            .BindAsync(query => sender.Send(query, cancellationToken))
            .MapAsync(orders => orders.Select(MapToDto).ToList())
            .ToActionResultAsync(this);

    /// <summary>
    /// List overdue orders.
    /// </summary>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrderDto>>> ListOverdueOrders(CancellationToken cancellationToken) =>
        await sender.Send(new ListOverdueOrdersQuery(), cancellationToken)
            .MapAsync(orders => orders.Select(MapToDto).ToList())
            .ToActionResultAsync(this);

    private static OrderDto MapToDto(Order o) => new(
        o.Id.Value,
        o.CustomerId.Value,
        o.CreatedByActorId,
        o.Status.Value,
        o.CreatedAt,
        o.SubmittedAt.HasValue ? o.SubmittedAt.Value : null,
        o.ShippedAt.HasValue ? o.ShippedAt.Value : null,
        o.LineItems.Select(li => new LineItemDto(
            li.Id.Value,
            li.ProductId.Value,
            li.ProductName,
            li.Quantity,
            li.UnitPrice.Amount,
            li.UnitPrice.Currency.Value,
            li.Total.Amount)).ToList());
}
