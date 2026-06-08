namespace OrderManagement.Domain;

/// <summary>Customer first name. 1–100 characters.</summary>
[StringLength(100)]
public partial class FirstName : RequiredString<FirstName>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "First name cannot be empty or whitespace.";
    }
}
