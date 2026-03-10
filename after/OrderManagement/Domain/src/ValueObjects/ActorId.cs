namespace OrderManagement.Domain.ValueObjects;

[StringLength(200)]
public partial class ActorId : RequiredString<ActorId>;
