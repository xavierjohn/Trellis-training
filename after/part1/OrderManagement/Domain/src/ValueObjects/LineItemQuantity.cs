namespace OrderManagement.Domain;

/// <summary>
/// Quantity of a line item. Between 1 and 999 inclusive.
/// </summary>
[Range(1, 999)]
public partial class LineItemQuantity : RequiredInt<LineItemQuantity>
{
}
