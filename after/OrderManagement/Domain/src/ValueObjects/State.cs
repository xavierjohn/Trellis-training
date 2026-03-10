namespace OrderManagement.Domain.ValueObjects;

[StringLength(100)]
public partial class State : RequiredString<State>;
