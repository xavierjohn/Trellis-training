namespace OrderManagement.Domain;

/// <summary>Product unit price in USD. Must be strictly greater than zero.</summary>
[Positive]
public partial class UnitPrice : RequiredDecimal<UnitPrice>;
