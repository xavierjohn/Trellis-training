namespace OrderManagement.Domain;

/// <summary>
/// Customer last name. 1–100 characters.
/// </summary>
[StringLength(100)]
public partial class LastName : RequiredString<LastName>
{
}
