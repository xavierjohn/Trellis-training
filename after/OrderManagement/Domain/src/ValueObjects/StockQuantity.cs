namespace OrderManagement.Domain;

/// <summary>Product stock quantity. Non-negative integer (zero allowed for empty inventory).</summary>
[NonNegative]
public partial class StockQuantity : RequiredInt<StockQuantity>;
