namespace OrderManagement.Domain;

/// <summary>Country component of a shipping address. 1–100 characters.</summary>
[Trim, NotDefault, StringLength(100)]
public partial class Country : RequiredString<Country>;
