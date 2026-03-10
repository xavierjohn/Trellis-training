namespace OrderManagement.Domain.ValueObjects;

[StringLength(200)]
public partial class Street : RequiredString<Street>;
