namespace OrderManagement.Domain;

using System.Text.RegularExpressions;

/// <summary>
/// Stock Keeping Unit. 3–20 characters, uppercase alphanumeric only.
/// </summary>
[StringLength(20)]
public partial class Sku : RequiredString<Sku>
{
    private static readonly Regex SkuPattern = new(@"^[A-Z0-9]{3,20}$", RegexOptions.Compiled);

    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrEmpty(value) || !SkuPattern.IsMatch(value))
            errorMessage = "SKU must be 3–20 characters of uppercase letters and digits only.";
    }
}
