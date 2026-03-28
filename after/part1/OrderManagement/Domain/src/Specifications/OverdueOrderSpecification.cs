namespace OrderManagement.Domain;

using System.Linq.Expressions;

/// <summary>
/// Matches orders that are overdue: in Submitted status for more than 7 days without being Approved.
/// </summary>
public class OverdueOrderSpecification : Specification<Order>
{
    private readonly DateTime _cutoff;

    /// <summary>
    /// Creates a specification that checks for overdue orders. An order is overdue if it has been
    /// in Submitted status for more than 7 days.
    /// </summary>
    public OverdueOrderSpecification(DateTime utcNow) => _cutoff = utcNow.AddDays(-7);

    /// <inheritdoc />
    public override Expression<Func<Order, bool>> ToExpression() =>
        order => order.Status == OrderStatus.Submitted
              && order.SubmittedAt.HasValue
              && order.SubmittedAt.GetValueOrDefault(DateTime.MaxValue) < _cutoff;
}
