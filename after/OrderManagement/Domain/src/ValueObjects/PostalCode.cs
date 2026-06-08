namespace OrderManagement.Domain;

/// <summary>Postal code component of a shipping address. 1–20 characters.</summary>
[StringLength(20)]
public partial class PostalCode : RequiredString<PostalCode>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "Postal code cannot be empty or whitespace.";
    }
}
