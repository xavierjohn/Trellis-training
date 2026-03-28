namespace OrderManagement.Domain;

/// <summary>Quantity of a product in an order line item. Must be between 1 and 999.</summary>
[Range(1, 999)]
public partial class LineItemQuantity : RequiredInt<LineItemQuantity>
{
}
