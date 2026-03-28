namespace OrderManagement.Domain;

/// <summary>Product display name. 1–200 characters.</summary>
[StringLength(200)]
public partial class ProductName : RequiredString<ProductName>
{
}
