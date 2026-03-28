namespace OrderManagement.Domain;

/// <summary>
/// Represents the lifecycle state of an order.
/// </summary>
public enum OrderStatus
{
    Draft,
    Submitted,
    Approved,
    Shipped,
    Delivered,
    Cancelled,
    Returned
}
