namespace OrderManagement.Domain;

/// <summary>
/// Status of an order. Discrete enum via <see cref="RequiredEnum{TSelf}"/> so the
/// state machine can pattern-match by static-instance reference and EF Core can
/// persist a stable string name.
/// </summary>
public partial class OrderStatus : RequiredEnum<OrderStatus>
{
    /// <summary>The initial state. Order is being assembled; line items may be added/removed.</summary>
    public static readonly OrderStatus Draft = new();

    /// <summary>The order has been submitted for approval; stock has been reserved.</summary>
    public static readonly OrderStatus Submitted = new();

    /// <summary>The order has been approved by a warehouse manager.</summary>
    public static readonly OrderStatus Approved = new();

    /// <summary>The order has been shipped.</summary>
    public static readonly OrderStatus Shipped = new();

    /// <summary>The order has been delivered to the customer (terminal).</summary>
    public static readonly OrderStatus Delivered = new();

    /// <summary>The order has been cancelled (terminal).</summary>
    public static readonly OrderStatus Cancelled = new();
}
