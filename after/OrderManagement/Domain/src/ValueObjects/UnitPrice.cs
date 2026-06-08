namespace OrderManagement.Domain;

/// <summary>Product unit price in USD. Must be strictly greater than zero.</summary>
public partial class UnitPrice : RequiredDecimal<UnitPrice>
{
    static partial void ValidateAdditional(decimal value, string fieldName, ref string? errorMessage)
    {
        if (value <= 0m)
            errorMessage = "Unit price must be greater than zero.";
    }
}
