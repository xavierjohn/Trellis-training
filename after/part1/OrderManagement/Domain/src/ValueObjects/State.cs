namespace OrderManagement.Domain;

[StringLength(100)]
public partial class State : RequiredString<State>
{
}
