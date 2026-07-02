namespace OrderManagement.Domain;

/// <summary>Unique identifier for an Order.</summary>
[NotDefault]
public partial class OrderId : RequiredGuid<OrderId>;
