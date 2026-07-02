namespace OrderManagement.Domain;

/// <summary>Customer first name. 1–100 characters.</summary>
[Trim, NotDefault, StringLength(100)]
public partial class FirstName : RequiredString<FirstName>;
