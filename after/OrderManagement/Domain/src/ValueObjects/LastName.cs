namespace OrderManagement.Domain;

/// <summary>Customer last name. 1–100 characters.</summary>
[StringLength(100)]
public partial class LastName : RequiredString<LastName>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "Last name cannot be empty or whitespace.";
    }
}
