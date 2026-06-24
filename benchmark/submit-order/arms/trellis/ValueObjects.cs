namespace TrellisArm;

// ── Value objects ──────────────────────────────────────────────────────────
// IDs and the two quantities that carry invariants. StockQuantity (>= 0) and
// Quantity (>= 1) make "negative stock" and "zero-quantity line" unrepresentable
// — the R6 invariant — and OrderStatus is a discrete enum the state machine
// pattern-matches by reference.

/// <summary>Unique identifier for a <see cref="Product"/>.</summary>
public partial class ProductId : RequiredGuid<ProductId>;

/// <summary>Unique identifier for an <see cref="Order"/>.</summary>
public partial class OrderId : RequiredGuid<OrderId>;

/// <summary>Unique identifier for a <see cref="LineItem"/>.</summary>
public partial class LineItemId : RequiredGuid<LineItemId>;

/// <summary>Identifier of the customer who owns an order.</summary>
public partial class CustomerId : RequiredGuid<CustomerId>;

/// <summary>Non-negative stock count. Zero is valid (an empty shelf); negative is not.</summary>
public partial class StockQuantity : RequiredInt<StockQuantity>
{
    static partial void ValidateAdditional(int value, string fieldName, ref string? errorMessage)
    {
        if (value < 0)
            errorMessage = "Stock quantity cannot be negative.";
    }
}

/// <summary>Line-item quantity in the range [1, 999].</summary>
public partial class Quantity : RequiredInt<Quantity>
{
    static partial void ValidateAdditional(int value, string fieldName, ref string? errorMessage)
    {
        if (value < 1 || value > 999)
            errorMessage = "Quantity must be between 1 and 999.";
    }
}

/// <summary>The order lifecycle states the state machine moves between.</summary>
public partial class OrderStatus : RequiredEnum<OrderStatus>
{
    /// <summary>Being assembled; line items may change; the only submittable state.</summary>
    public static readonly OrderStatus Draft = new();

    /// <summary>Submitted for fulfilment; stock has been reserved (terminal for this benchmark).</summary>
    public static readonly OrderStatus Submitted = new();

    /// <summary>Cancelled (terminal).</summary>
    public static readonly OrderStatus Cancelled = new();
}
