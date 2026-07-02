namespace OrderManagement.Domain;

/// <summary>Product name. 1–200 characters.</summary>
[Trim, NotDefault, StringLength(200)]
public partial class ProductName : RequiredString<ProductName>;
