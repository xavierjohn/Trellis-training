namespace OrderManagement.Domain;

[Range(0, int.MaxValue)]
public partial class StockQuantity : RequiredInt<StockQuantity>
{
}
