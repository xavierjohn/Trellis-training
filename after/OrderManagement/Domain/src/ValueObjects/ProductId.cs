namespace OrderManagement.Domain;

/// <summary>Unique identifier for a Product.</summary>
[NotDefault]
public partial class ProductId : RequiredGuid<ProductId>;
