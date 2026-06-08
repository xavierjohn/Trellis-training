namespace OrderManagement.Domain;

/// <summary>Unique identifier for a LineItem.</summary>
public partial class LineItemId : RequiredGuid<LineItemId>
{
    static partial void ValidateAdditional(Guid value, string fieldName, ref string? errorMessage)
    {
        if (value == Guid.Empty)
            errorMessage = "LineItem Id cannot be empty.";
    }
}
