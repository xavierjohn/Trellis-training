namespace OrderManagement.Domain.ValueObjects;

using System.Text.RegularExpressions;

public partial class Sku : ScalarValueObject<Sku, string>, IScalarValue<Sku, string>
{
    private static readonly Regex SkuPattern = new("^[A-Z0-9]{3,20}$", RegexOptions.Compiled);

    private Sku(string value) : base(value) { }

    public static Result<Sku> TryCreate(string? value, string? fieldName = null)
    {
        fieldName ??= "Sku";

        if (string.IsNullOrWhiteSpace(value))
        {
            return Error.Validation($"{fieldName} is required.", fieldName);
        }

        var trimmed = value.Trim().ToUpperInvariant();

        if (!SkuPattern.IsMatch(trimmed))
        {
            return Error.Validation($"{fieldName} must be 3-20 uppercase alphanumeric characters.", fieldName);
        }

        return new Sku(trimmed);
    }
}
