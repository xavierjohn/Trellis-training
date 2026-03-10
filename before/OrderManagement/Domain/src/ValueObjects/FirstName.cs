namespace OrderManagement.Domain;

[StringLength(50)]
public partial class FirstName : RequiredString<FirstName>
{
}
