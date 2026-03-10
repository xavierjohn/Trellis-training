namespace OrderManagement.Domain.ValueObjects;

public partial class LineItemQuantity : ScalarValueObject<LineItemQuantity, int>, IScalarValue<LineItemQuantity, int>
{
    private LineItemQuantity(int value) : base(value) { }

    public static Result<LineItemQuantity> TryCreate(int value, string? fieldName = null)
    {
        fieldName ??= "Quantity";

        if (value < 1)
        {
            return Error.Validation($"{fieldName} must be at least 1.", fieldName);
        }

        if (value > 999)
        {
            return Error.Validation($"{fieldName} must be at most 999.", fieldName);
        }

        return new LineItemQuantity(value);
    }
}
