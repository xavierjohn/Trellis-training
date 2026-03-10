namespace OrderManagement.Domain.ValueObjects;

[StringLength(100)]
public partial class CustomerFirstName : RequiredString<CustomerFirstName>;
