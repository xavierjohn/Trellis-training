namespace OrderManagement.Domain.ValueObjects;

[StringLength(20)]
public partial class PostalCode : RequiredString<PostalCode>;
