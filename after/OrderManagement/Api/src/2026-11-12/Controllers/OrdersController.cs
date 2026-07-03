namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Asp;
using Trellis.Asp.ApiVersioning;

/// <summary>Orders controller (spec §6.4–§6.12, §6.14, §7).</summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender) => _sender = sender;

    /// <summary>Create a draft order. <c>POST /api/orders</c>.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken) =>
        _sender.Send(
                new CreateDraftOrderCommand(
                    request.CustomerId,
                    request.LineItems.Select(li => li.ToDomain()).ToList()),
                cancellationToken)
            .ToHttpResponseAsync(
                OrderResponse.From,
                opts => opts
                    .CreatedAtRoute("Orders_GetById", o => new Microsoft.AspNetCore.Routing.RouteValueDictionary
                    {
                        ["id"] = o.Id.Value,
                    })
                    .WithVersionedRoute())
            .AsActionResultAsync<OrderResponse>();

    /// <summary>Get an order by id. <c>GET /api/orders/{id}</c>. Emits a strong ETag and honors <c>If-None-Match</c> (304).</summary>
    [HttpGet("{id}", Name = "Orders_GetById")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> GetById(
        OrderId id,
        CancellationToken cancellationToken) =>
        _sender.Send(new GetOrderByIdQuery(id), cancellationToken)
            .ToHttpResponseAsync(
                OrderResponse.From,
                opts => opts
                    .WithETag(o => o.ETag)
                    .WithLastModified(o => o.LastModified)
                    .EvaluatePreconditions())
            .AsActionResultAsync<OrderResponse>();

    /// <summary>List overdue orders as a bounded page. <c>GET /api/orders/overdue</c>.</summary>
    [HttpGet("overdue", Name = "Orders_GetOverdue")]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ValueTask<ActionResult<PagedResponse<OrderResponse>>> GetOverdue(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken) =>
        _sender.Send(new ListOverdueOrdersQuery(cursor, limit), cancellationToken)
            .ToHttpResponseAsync(
                HttpContext.PageUrl("Orders_GetOverdue", (next, applied) =>
                    new Microsoft.AspNetCore.Routing.RouteValueDictionary
                    {
                        ["cursor"] = next.Token,
                        ["limit"] = applied,
                    }),
                OrderResponse.From)
            .AsActionResultAsync<PagedResponse<OrderResponse>>();

    /// <summary>
    /// Add a line item to a draft order. <c>POST /api/orders/{id}/line-items</c>.
    /// Optimistic concurrency: requires an <c>If-Match</c> ETag (428 if absent, 412 if stale).
    /// </summary>
    [HttpPost("{id}/line-items")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public ValueTask<ActionResult<OrderResponse>> AddLineItem(
        OrderId id,
        [FromBody] AddLineItemRequest request,
        CancellationToken cancellationToken) =>
        _sender.Send(
                new AddLineItemCommand(id, request.ProductId, request.Quantity, ETagHelper.ParseIfMatch(Request)),
                cancellationToken)
            .ToHttpResponseAsync(
                OrderResponse.From,
                opts => opts.WithETag(o => o.ETag))
            .AsActionResultAsync<OrderResponse>();

    /// <summary>
    /// Remove a line item from a draft order.
    /// <c>DELETE /api/orders/{id}/line-items/{lineItemId}</c>.
    /// </summary>
    [HttpDelete("{id}/line-items/{lineItemId}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> RemoveLineItem(
        OrderId id,
        LineItemId lineItemId,
        CancellationToken cancellationToken) =>
        _sender.Send(new RemoveLineItemCommand(id, lineItemId), cancellationToken)
            .ToHttpResponseAsync(OrderResponse.From)
            .AsActionResultAsync<OrderResponse>();

    /// <summary>Submit an order. <c>POST /api/orders/{id}/submission</c>.</summary>
    [HttpPost("{id}/submission")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> Submit(
        OrderId id,
        CancellationToken cancellationToken) =>
        _sender.Send(new SubmitOrderCommand(id), cancellationToken)
            .ToHttpResponseAsync(OrderResponse.From)
            .AsActionResultAsync<OrderResponse>();

    /// <summary>Approve an order. <c>POST /api/orders/{id}/approval</c>.</summary>
    [HttpPost("{id}/approval")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> Approve(
        OrderId id,
        CancellationToken cancellationToken) =>
        _sender.Send(new ApproveOrderCommand(id), cancellationToken)
            .ToHttpResponseAsync(OrderResponse.From)
            .AsActionResultAsync<OrderResponse>();

    /// <summary>Ship an order. <c>POST /api/orders/{id}/shipment</c>.</summary>
    [HttpPost("{id}/shipment")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> Ship(
        OrderId id,
        CancellationToken cancellationToken) =>
        _sender.Send(new ShipOrderCommand(id), cancellationToken)
            .ToHttpResponseAsync(OrderResponse.From)
            .AsActionResultAsync<OrderResponse>();

    /// <summary>Mark an order as delivered. <c>POST /api/orders/{id}/delivery</c>.</summary>
    [HttpPost("{id}/delivery")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> Deliver(
        OrderId id,
        CancellationToken cancellationToken) =>
        _sender.Send(new DeliverOrderCommand(id), cancellationToken)
            .ToHttpResponseAsync(OrderResponse.From)
            .AsActionResultAsync<OrderResponse>();

    /// <summary>
    /// Cancel an order. <c>POST /api/orders/{id}/cancellation</c>.
    /// <para>
    /// Requires <c>orders:cancel</c> AND ownership (or <c>orders:read-all</c>).
    /// The resource-authorization pipeline loads the <see cref="Order"/> once via
    /// <c>SharedResourceLoaderById&lt;Order, OrderId&gt;</c>; the handler re-uses
    /// the loaded instance via the v4 typed
    /// <c>IAuthorizedResource&lt;CancelOrderCommand, Order&gt;</c> accessor.
    /// </para>
    /// </summary>
    [HttpPost("{id}/cancellation")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ValueTask<ActionResult<OrderResponse>> Cancel(
        OrderId id,
        CancellationToken cancellationToken) =>
        _sender.Send(new CancelOrderCommand(id), cancellationToken)
            .ToHttpResponseAsync(OrderResponse.From)
            .AsActionResultAsync<OrderResponse>();
}
