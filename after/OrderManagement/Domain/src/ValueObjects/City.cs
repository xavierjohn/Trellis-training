namespace OrderManagement.Domain.ValueObjects;

[StringLength(100)]
public partial class City : RequiredString<City>;
