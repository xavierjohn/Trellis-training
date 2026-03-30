#pragma warning disable CS1591
namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using ServiceLevelIndicators;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Asp;

[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender) => _sender = sender;

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> CreateDraftOrder(
        [FromBody] CreateDraftOrderRequest request,
        CancellationToken cancellationToken)
    {
        var lineItems = request.LineItems
            .Select(li => new CreateDraftOrderLineItemInput(li.ProductId, li.Quantity))
            .ToList();

        return await _sender.Send(
            new CreateDraftOrderCommand(request.CustomerId, lineItems),
            cancellationToken)
            .ToCreatedAtActionResultAsync(this, nameof(GetOrder), o => new { id = (Guid)o.Id }, OrderResponse.From);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> GetOrder(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new GetOrderByIdQuery(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpPost("{id}/line-items")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> AddLineItem(
        [CustomerResourceId] OrderId id,
        [FromBody] AddLineItemRequest request,
        CancellationToken cancellationToken) =>
        await _sender.Send(new AddLineItemCommand(id, request.ProductId, request.Quantity), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpDelete("{id}/line-items/{lineItemId}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> RemoveLineItem(
        [CustomerResourceId] OrderId id,
        LineItemId lineItemId,
        CancellationToken cancellationToken) =>
        await _sender.Send(new RemoveLineItemCommand(id, lineItemId), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpPost("{id}/submission")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> SubmitOrder(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new SubmitOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpPost("{id}/approval")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> ApproveOrder(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new ApproveOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpPost("{id}/shipment")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> ShipOrder(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new ShipOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpPost("{id}/delivery")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> DeliverOrder(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new DeliverOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpPost("{id}/cancellation")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<OrderResponse>> CancelOrder(
        [CustomerResourceId] OrderId id,
        CancellationToken cancellationToken) =>
        await _sender.Send(new CancelOrderCommand(id), cancellationToken)
            .ToActionResultAsync(this, OrderResponse.From);

    [HttpGet("overdue")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async ValueTask<ActionResult<IReadOnlyList<OrderResponse>>> ListOverdueOrders(
        CancellationToken cancellationToken) =>
        await _sender.Send(new ListOverdueOrdersQuery(), cancellationToken)
            .ToActionResultAsync(this, orders => (IReadOnlyList<OrderResponse>)orders.Select(OrderResponse.From).ToList());
}
