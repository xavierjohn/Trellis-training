namespace OrderManagement.Domain;

/// <summary>Customer first name. 1–100 characters.</summary>
[StringLength(100)]
public partial class CustomerFirstName : RequiredString<CustomerFirstName>
{
}
