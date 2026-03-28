namespace OrderManagement.Api.v2026_11_12.Models;

/// <summary>
/// Request model for returning an order.
/// </summary>
public record ReturnOrderRequest
{
    /// <summary>Reason for the return. 10–500 characters.</summary>
    public string Reason { get; init; } = null!;
}
