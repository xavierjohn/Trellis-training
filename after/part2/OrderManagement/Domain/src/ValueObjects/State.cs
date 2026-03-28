namespace OrderManagement.Domain;

/// <summary>
/// State or province.
/// </summary>
[StringLength(100)]
public partial class State : RequiredString<State>
{
}
