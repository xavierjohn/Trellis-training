namespace OrderManagement.Domain;

using System.Linq.Expressions;

/// <summary>
/// Matches orders that are overdue: in Submitted status for more than 7 days without being approved.
/// </summary>
public class OverdueOrderSpecification : Specification<Order>
{
    private readonly DateTime _asOf;

    /// <summary>
    /// Creates a specification that checks for overdue orders relative to the given date.
    /// Orders submitted before this cutoff date are considered overdue.
    /// </summary>
    public OverdueOrderSpecification(DateTime asOf) => _asOf = asOf;

    /// <inheritdoc />
    public override Expression<Func<Order, bool>> ToExpression() =>
        order => order.Status == OrderStatus.Submitted &&
                 order.SubmittedAt.HasValue &&
                 order.SubmittedAt.GetValueOrDefault(DateTime.MaxValue) < _asOf;
}
