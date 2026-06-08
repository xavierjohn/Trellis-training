namespace OrderManagement.Domain;

/// <summary>Product name. 1–200 characters.</summary>
[StringLength(200)]
public partial class ProductName : RequiredString<ProductName>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "Product name cannot be empty or whitespace.";
    }
}
