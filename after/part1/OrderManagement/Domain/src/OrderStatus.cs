namespace OrderManagement.Domain;

/// <summary>Lifecycle status of an order.</summary>
public enum OrderStatus
{
    Draft,
    Submitted,
    Approved,
    Shipped,
    Delivered,
    Cancelled
}
