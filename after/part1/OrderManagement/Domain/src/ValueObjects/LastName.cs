namespace OrderManagement.Domain;

[StringLength(100)]
public partial class LastName : RequiredString<LastName>
{
}
