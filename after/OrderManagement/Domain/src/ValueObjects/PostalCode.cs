namespace OrderManagement.Domain;

/// <summary>Postal code component of a shipping address. 1–20 characters.</summary>
[Trim, NotDefault, StringLength(20)]
public partial class PostalCode : RequiredString<PostalCode>;
