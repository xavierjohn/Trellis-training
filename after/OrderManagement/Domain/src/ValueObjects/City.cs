namespace OrderManagement.Domain;

/// <summary>City component of a shipping address. 1–100 characters.</summary>
[StringLength(100)]
public partial class City : RequiredString<City>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "City cannot be empty or whitespace.";
    }
}
