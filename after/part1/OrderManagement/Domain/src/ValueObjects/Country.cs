namespace OrderManagement.Domain;

[StringLength(100)]
public partial class Country : RequiredString<Country>
{
}
