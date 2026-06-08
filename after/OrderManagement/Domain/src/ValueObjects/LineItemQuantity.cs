namespace OrderManagement.Domain;

/// <summary>Line-item quantity. Integer in range [1, 999].</summary>
public partial class LineItemQuantity : RequiredInt<LineItemQuantity>
{
    /// <summary>Maximum quantity per line item.</summary>
    public const int Max = 999;

    /// <summary>Minimum quantity per line item.</summary>
    public const int Min = 1;

    static partial void ValidateAdditional(int value, string fieldName, ref string? errorMessage)
    {
        if (value < Min || value > Max)
            errorMessage = $"Line-item quantity must be between {Min} and {Max}.";
    }
}
