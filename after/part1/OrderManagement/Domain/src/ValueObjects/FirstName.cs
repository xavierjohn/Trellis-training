namespace OrderManagement.Domain;

[StringLength(100)]
public partial class FirstName : RequiredString<FirstName>
{
}
