namespace OrderManagement.Domain;

/// <summary>
/// Country name.
/// </summary>
[StringLength(100)]
public partial class Country : RequiredString<Country>
{
}
