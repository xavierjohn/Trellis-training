namespace OrderManagement.Domain;

/// <summary>
/// An external payment gateway reference recorded when an order's payment is confirmed
/// (for example the gateway's transaction id). Trimmed, non-empty, at most 200 characters.
/// </summary>
[Trim, NotDefault, StringLength(200)]
public partial class PaymentRef : RequiredString<PaymentRef>;
