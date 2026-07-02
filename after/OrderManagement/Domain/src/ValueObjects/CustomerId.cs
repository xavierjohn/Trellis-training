namespace OrderManagement.Domain;

/// <summary>Unique identifier for a Customer.</summary>
[NotDefault]
public partial class CustomerId : RequiredGuid<CustomerId>;
