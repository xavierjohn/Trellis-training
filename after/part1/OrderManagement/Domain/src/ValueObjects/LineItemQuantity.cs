namespace OrderManagement.Domain;

[Range(1, 999)]
public partial class LineItemQuantity : RequiredInt<LineItemQuantity>
{
}
