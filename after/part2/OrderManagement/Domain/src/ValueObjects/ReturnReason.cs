namespace OrderManagement.Domain;

public sealed class ReturnReason : ScalarValueObject<ReturnReason, string>, IScalarValue<ReturnReason, string>
{
    private ReturnReason(string value) : base(value) { }

    public static Result<ReturnReason> TryCreate(string? value, string? fieldName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error.Validation("Return reason is required", fieldName ?? "reason");

        var trimmed = value.Trim();

        if (trimmed.Length < 10)
            return Error.Validation("Return reason must be at least 10 characters", fieldName ?? "reason");

        if (trimmed.Length > 500)
            return Error.Validation("Return reason must be no more than 500 characters", fieldName ?? "reason");

        return new ReturnReason(trimmed);
    }
}
