namespace OrderManagement.Domain;

/// <summary>Country component of a shipping address. 1–100 characters.</summary>
[StringLength(100)]
public partial class Country : RequiredString<Country>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "Country cannot be empty or whitespace.";
    }
}
