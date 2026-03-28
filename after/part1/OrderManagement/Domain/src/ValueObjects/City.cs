namespace OrderManagement.Domain;

/// <summary>
/// City name.
/// </summary>
[StringLength(100)]
public partial class City : RequiredString<City>
{
}
