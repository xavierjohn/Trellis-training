namespace OrderManagement.Domain;

public partial class OrderStatus : RequiredEnum<OrderStatus>
{
    public static readonly OrderStatus Draft = new();
    public static readonly OrderStatus Submitted = new();
    public static readonly OrderStatus Approved = new();
    public static readonly OrderStatus Shipped = new();
    public static readonly OrderStatus Delivered = new();
    public static readonly OrderStatus Cancelled = new();
}
