namespace OrderManagement.Domain;

/// <summary>Non-negative quantity of stock on hand for a product.</summary>
[Range(0, 1_000_000)]
public partial class StockQuantity : RequiredInt<StockQuantity>
{
}
