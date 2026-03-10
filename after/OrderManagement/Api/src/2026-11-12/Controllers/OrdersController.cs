namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.ValueObjects;
using Trellis.Asp;

[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateDraftOrderRequest request,
        CancellationToken ct)
    {
        var lineItemInputs = new List<LineItemInput>();
        foreach (var item in request.LineItems)
        {
            var quantityResult = LineItemQuantity.TryCreate(item.Quantity);
            if (quantityResult.TryGetError(out var error))
            {
                return error.ToActionResult<OrderResponse>(this);
            }

            quantityResult.TryGetValue(out var quantity);
            lineItemInputs.Add(new LineItemInput(item.ProductId, quantity));
        }

        var command = new CreateDraftOrderCommand(request.CustomerId, lineItemInputs);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToCreatedAtActionResultAsync(this, nameof(GetById), dto => new { id = dto.Id });
    }

    [HttpPost("{id}/line-items")]
    public async Task<ActionResult<OrderResponse>> AddLineItem(
        OrderId id,
        [FromBody] AddLineItemRequest request,
        CancellationToken ct)
    {
        var quantityResult = LineItemQuantity.TryCreate(request.Quantity);
        if (quantityResult.TryGetError(out var error))
        {
            return error.ToActionResult<OrderResponse>(this);
        }

        quantityResult.TryGetValue(out var quantity);
        var command = new AddLineItemCommand(id, request.ProductId, quantity);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpDelete("{id}/line-items/{lineItemId}")]
    public async Task<ActionResult<OrderResponse>> RemoveLineItem(
        OrderId id,
        LineItemId lineItemId,
        CancellationToken ct)
    {
        var command = new RemoveLineItemCommand(id, lineItemId);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpPost("{id}/submission")]
    public async Task<ActionResult<OrderResponse>> Submit(
        OrderId id,
        CancellationToken ct)
    {
        var command = new SubmitOrderCommand(id);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpPost("{id}/approval")]
    public async Task<ActionResult<OrderResponse>> Approve(
        OrderId id,
        CancellationToken ct)
    {
        var command = new ApproveOrderCommand(id);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpPost("{id}/shipment")]
    public async Task<ActionResult<OrderResponse>> Ship(
        OrderId id,
        CancellationToken ct)
    {
        var command = new ShipOrderCommand(id);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpPost("{id}/delivery")]
    public async Task<ActionResult<OrderResponse>> Deliver(
        OrderId id,
        CancellationToken ct)
    {
        var command = new DeliverOrderCommand(id);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpPost("{id}/cancellation")]
    public async Task<ActionResult<OrderResponse>> Cancel(
        OrderId id,
        CancellationToken ct)
    {
        var command = new CancelOrderCommand(id);

        return await sender.Send(command, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetById(
        OrderId id,
        CancellationToken ct)
    {
        var query = new GetOrderByIdQuery(id);

        return await sender.Send(query, ct)
            .MapAsync(OrderResponse.From)
            .ToActionResultAsync(this);
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<List<OrderResponse>>> GetOverdue(CancellationToken ct)
    {
        var query = new ListOverdueOrdersQuery();

        return await sender.Send(query, ct)
            .MapAsync(orders => orders.Select(OrderResponse.From).ToList())
            .ToActionResultAsync(this);
    }
}
