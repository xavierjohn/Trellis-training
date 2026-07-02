namespace OrderManagement.Domain;

/// <summary>Unique identifier for a LineItem.</summary>
[NotDefault]
public partial class LineItemId : RequiredGuid<LineItemId>;
