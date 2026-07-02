namespace OrderManagement.Domain;

/// <summary>Street component of a shipping address. 1–200 characters.</summary>
[Trim, NotDefault, StringLength(200)]
public partial class Street : RequiredString<Street>;
