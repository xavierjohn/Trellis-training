namespace OrderManagement.Domain.ValueObjects;

[StringLength(100)]
public partial class CustomerLastName : RequiredString<CustomerLastName>;
