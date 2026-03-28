namespace OrderManagement.Domain;

/// <summary>
/// Current stock quantity of a product. Non-negative integer.
/// </summary>
[Range(0, int.MaxValue)]
public partial class StockQuantity : RequiredInt<StockQuantity>
{
}
