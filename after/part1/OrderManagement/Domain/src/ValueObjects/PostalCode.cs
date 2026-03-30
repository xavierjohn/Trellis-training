namespace OrderManagement.Domain;

[StringLength(20)]
public partial class PostalCode : RequiredString<PostalCode>
{
}
