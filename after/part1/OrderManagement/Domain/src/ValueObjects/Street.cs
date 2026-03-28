namespace OrderManagement.Domain;

/// <summary>
/// Street address.
/// </summary>
[StringLength(200)]
public partial class Street : RequiredString<Street>
{
}
