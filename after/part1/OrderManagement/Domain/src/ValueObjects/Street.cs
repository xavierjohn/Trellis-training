namespace OrderManagement.Domain;

[StringLength(200)]
public partial class Street : RequiredString<Street>
{
}
