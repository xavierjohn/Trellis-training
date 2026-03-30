namespace OrderManagement.Domain;

[StringLength(100)]
public partial class City : RequiredString<City>
{
}
