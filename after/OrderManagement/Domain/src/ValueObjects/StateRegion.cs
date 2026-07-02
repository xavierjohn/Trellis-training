namespace OrderManagement.Domain;

/// <summary>State / province / region component of a shipping address. 1–100 characters.</summary>
[Trim, NotDefault, StringLength(100)]
public partial class StateRegion : RequiredString<StateRegion>;
