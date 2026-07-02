namespace OrderManagement.Domain;

/// <summary>City component of a shipping address. 1–100 characters.</summary>
[Trim, NotDefault, StringLength(100)]
public partial class City : RequiredString<City>;
