namespace OrderManagement.Domain;

/// <summary>
/// The opaque identifier of the actor performing an operation, sourced from
/// the JWT <c>sub</c> (or <c>oid</c>) claim. Captured on orders as
/// <see cref="Order.CreatedByActorId"/> for resource-authorization ownership checks.
/// </summary>
[StringLength(200)]
public partial class ActorId : RequiredString<ActorId>
{
    static partial void ValidateAdditional(string value, string fieldName, ref string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            errorMessage = "Actor Id cannot be empty or whitespace.";
    }
}
