namespace OrderManagement.Domain;

using System.Text.RegularExpressions;

/// <summary>
/// Stock Keeping Unit — 3–20 characters, uppercase letters, digits, and hyphens only.
/// No leading/trailing hyphens.
/// </summary>
[StringLength(20, MinimumLength = 3)]
public partial class Sku : RequiredString<Sku>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (!SkuPattern().IsMatch(value))
            errorMessage = "SKU must be 3–20 characters, uppercase letters, digits, and hyphens only, with no leading/trailing hyphens.";
    }

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9\-]{1,18}[A-Z0-9]$")]
    private static partial Regex SkuPattern();
}
