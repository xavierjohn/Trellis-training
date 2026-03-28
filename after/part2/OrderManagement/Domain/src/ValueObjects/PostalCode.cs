namespace OrderManagement.Domain;

/// <summary>
/// Postal or zip code.
/// </summary>
[StringLength(20)]
public partial class PostalCode : RequiredString<PostalCode>
{
}
