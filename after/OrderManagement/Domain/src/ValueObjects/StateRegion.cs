namespace OrderManagement.Domain;

/// <summary>State / province / region component of a shipping address. 1–100 characters.</summary>
[StringLength(100)]
public partial class StateRegion : RequiredString<StateRegion>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "State cannot be empty or whitespace.";
    }
}
