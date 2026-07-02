namespace OrderManagement.AntiCorruptionLayer.Eventing;

using Microsoft.Extensions.Logging;
using OrderManagement.Application.IntegrationEvents;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Mediator;

/// <summary>
/// Consumes a <see cref="PaymentConfirmedIntegrationEvent"/> and records the payment against the order.
/// Payment is recorded ONLY for a Submitted order whose total matches the confirmed USD amount.
/// Every other outcome — wrong currency, unknown / non-Submitted (incl. cancelled) order, malformed
/// payload, amount mismatch, or a conflicting duplicate — is logged and acknowledged, so the broker
/// does not redeliver a poison message forever. The inbox dispatcher owns the commit; this handler only
/// stages changes and is idempotent (the inbox de-duplicates redeliveries and <see cref="Order.RecordPayment"/>
/// no-ops an exact duplicate).
/// </summary>
internal sealed partial class PaymentConfirmedHandler(
    IOrderRepository orderRepository,
    ILogger<PaymentConfirmedHandler> logger) : IIntegrationEventHandler<PaymentConfirmedIntegrationEvent>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(PaymentConfirmedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (!string.Equals(integrationEvent.Currency, "USD", StringComparison.Ordinal))
        {
            LogWrongCurrency(logger, integrationEvent.OrderId, integrationEvent.Currency);
            return;
        }

        if (!OrderId.TryCreate(integrationEvent.OrderId).TryGetValue(out var orderId))
        {
            LogInvalidOrderId(logger, integrationEvent.OrderId);
            return;
        }

        var orderMaybe = await orderRepository.FindByIdAsync(orderId, cancellationToken);
        if (!orderMaybe.TryGetValue(out var order))
        {
            LogUnknownOrder(logger, integrationEvent.OrderId);
            return;
        }

        if (order.Status != OrderStatus.Submitted)
        {
            LogNotSubmitted(logger, integrationEvent.OrderId, order.Status.Value);
            return;
        }

        if (!PaymentRef.TryCreate(integrationEvent.PaymentReference).TryGetValue(out var paymentRef))
        {
            LogMalformedPayload(logger, integrationEvent.OrderId);
            return;
        }

        var amountPaid = integrationEvent.AmountPaid;
        if (amountPaid != order.OrderTotal)
        {
            LogAmountMismatch(logger, integrationEvent.OrderId, integrationEvent.AmountPaid, order.OrderTotal);
            return;
        }

        var result = order.RecordPayment(paymentRef, amountPaid, integrationEvent.OccurredAt);
        if (result.IsFailure)
            LogConflictingPayment(logger, integrationEvent.OrderId, integrationEvent.PaymentReference);
    }

    [LoggerMessage(1, LogLevel.Warning, "PaymentConfirmed for order {OrderId} ignored: currency {Currency} is not USD.")]
    static partial void LogWrongCurrency(ILogger logger, Guid orderId, string currency);

    [LoggerMessage(2, LogLevel.Warning, "PaymentConfirmed for unknown order {OrderId} ignored.")]
    static partial void LogUnknownOrder(ILogger logger, Guid orderId);

    [LoggerMessage(3, LogLevel.Warning, "PaymentConfirmed for order {OrderId} ignored: order is {Status}, not Submitted.")]
    static partial void LogNotSubmitted(ILogger logger, Guid orderId, string status);

    [LoggerMessage(4, LogLevel.Warning, "PaymentConfirmed for order {OrderId} ignored: malformed payment reference.")]
    static partial void LogMalformedPayload(ILogger logger, Guid orderId);

    [LoggerMessage(5, LogLevel.Error, "PaymentConfirmed for order {OrderId} ignored: amount {AmountPaid} does not match order total {OrderTotal}.")]
    static partial void LogAmountMismatch(ILogger logger, Guid orderId, decimal amountPaid, decimal orderTotal);

    [LoggerMessage(6, LogLevel.Error, "PaymentConfirmed for order {OrderId} ignored: a different payment (ref {PaymentReference}) is already recorded.")]
    static partial void LogConflictingPayment(ILogger logger, Guid orderId, string paymentReference);

    [LoggerMessage(7, LogLevel.Warning, "PaymentConfirmed with an invalid order id {OrderId} ignored.")]
    static partial void LogInvalidOrderId(ILogger logger, Guid orderId);
}
