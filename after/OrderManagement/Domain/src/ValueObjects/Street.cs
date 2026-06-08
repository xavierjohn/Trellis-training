namespace OrderManagement.Domain;

/// <summary>Street component of a shipping address. 1–200 characters.</summary>
[StringLength(200)]
public partial class Street : RequiredString<Street>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "Street cannot be empty or whitespace.";
    }
}
