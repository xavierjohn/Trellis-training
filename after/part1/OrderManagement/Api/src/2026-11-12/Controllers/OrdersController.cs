namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using ServiceLevelIndicators;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Asp;

/// <summary>
/// Orders controller.
/// </summary>
[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>Constructor.</summary>
    public OrdersController(ISender sender) => _sender = sender;

    /// <summary>Create a new draft order.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken) =>
        await CreateDraftOrderCommand.TryCreate(
                request.CustomerId,
                request.LineItems
                    .Select(li => new CreateDraftOrderCommand.LineItemInput(li.ProductId, li.Quantity))
                    .ToList())
            .BindAsync(command => _sender.Send(command, cancellationToken))
            .ToCreatedAtActionResultAsync(this, nameof(GetById), o => new { id = (Guid)o.Id }, OrderResponse.From);

    /// <summary>Get an order by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> GetById(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new GetOrderByIdQuery(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    /// <summary>List overdue orders (submitted more than 7 days ago without approval).</summary>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async ValueTask<ActionResult<IReadOnlyList<OrderResponse>>> GetOverdue(
        CancellationToken cancellationToken) =>
        await _sender.Send(new ListOverdueOrdersQuery(), cancellationToken)
            .ToActionResultAsync(this, orders => (IReadOnlyList<OrderResponse>)orders.Select(OrderResponse.From).ToList());

    /// <summary>Add a line item to a draft order.</summary>
    [HttpPost("{id}/line-items")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> AddLineItem(
        [CustomerResourceId] OrderId id,
        [FromBody] AddLineItemRequest request,
        CancellationToken cancellationToken) =>
        await _sender.Send(new AddLineItemToDraftOrderCommand(id, request.ProductId, request.Quantity), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    /// <summary>Remove a line item from a draft order.</summary>
    [HttpDelete("{id}/line-items/{lineItemId}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> RemoveLineItem(
        [CustomerResourceId] OrderId id,
        LineItemId lineItemId,
        CancellationToken cancellationToken) =>
        await _sender.Send(new RemoveLineItemFromDraftOrderCommand(id, lineItemId), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    /// <summary>Submit a draft order.</summary>
    [HttpPost("{id}/submission")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> Submit(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new SubmitOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    /// <summary>Approve a submitted order.</summary>
    [HttpPost("{id}/approval")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> Approve(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new ApproveOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    /// <summary>Ship an approved order.</summary>
    [HttpPost("{id}/shipment")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> Ship(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new ShipOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    /// <summary>Mark a shipped order as delivered.</summary>
    [HttpPost("{id}/delivery")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> Deliver(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new DeliverOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    /// <summary>Cancel an order (subject to ownership check).</summary>
    [HttpPost("{id}/cancellation")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> Cancel(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new CancelOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);
}
