namespace OrderManagement.Domain;

/// <summary>
/// Reason for returning an order. 10–500 characters.
/// </summary>
[StringLength(500, MinimumLength = 10)]
public partial class ReturnReason : RequiredString<ReturnReason>
{
}
