namespace OrderManagement.Domain;

/// <summary>Unique identifier for a Product.</summary>
public partial class ProductId : RequiredGuid<ProductId>
{
    static partial void ValidateAdditional(Guid value, string fieldName, ref string? errorMessage)
    {
        if (value == Guid.Empty)
            errorMessage = "Product Id cannot be empty.";
    }
}
