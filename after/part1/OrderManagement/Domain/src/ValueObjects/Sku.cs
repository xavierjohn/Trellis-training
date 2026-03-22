namespace OrderManagement.Domain;

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

[JsonConverter(typeof(ParsableJsonConverter<Sku>))]
public sealed class Sku : ScalarValueObject<Sku, string>, IScalarValue<Sku, string>, IParsable<Sku>
{
    private static readonly Regex Pattern =
        new("^[A-Z0-9]{3,20}$", RegexOptions.Compiled);

    private Sku(string value) : base(value) { }

    public static Result<Sku> TryCreate(string? value, string? fieldName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error.Validation("SKU is required", fieldName ?? "sku");

        var trimmed = value.Trim();
        if (!Pattern.IsMatch(trimmed))
            return Error.Validation("SKU must be 3-20 uppercase alphanumeric characters", fieldName ?? "sku");

        return new Sku(trimmed);
    }

    public static Sku Parse(string s, IFormatProvider? provider = null)
    {
        if (TryCreate(s).TryGetValue(out var sku))
            return sku;
        throw new FormatException($"Cannot parse '{s}' as a valid SKU");
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out Sku result)
    {
        var r = TryCreate(s);
        if (r.IsSuccess)
        {
            result = r.Value;
            return true;
        }

        result = default!;
        return false;
    }
}
