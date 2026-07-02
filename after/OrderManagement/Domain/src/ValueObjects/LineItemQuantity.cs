namespace OrderManagement.Domain;

/// <summary>Line-item quantity. Integer in range [1, 999].</summary>
[Range(1, 999)]
public partial class LineItemQuantity : RequiredInt<LineItemQuantity>;
