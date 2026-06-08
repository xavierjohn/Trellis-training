namespace OrderManagement.Domain;

/// <summary>Product stock quantity. Non-negative integer.</summary>
public partial class StockQuantity : RequiredInt<StockQuantity>
{
    static partial void ValidateAdditional(int value, string fieldName, ref string? errorMessage)
    {
        if (value < 0)
            errorMessage = "Stock quantity cannot be negative.";
    }
}
