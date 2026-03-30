namespace OrderManagement.Domain;

[StringLength(200)]
public partial class ProductName : RequiredString<ProductName>
{
}
