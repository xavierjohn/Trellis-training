namespace OrderManagement.Domain;

using System.Linq.Expressions;

/// <summary>
/// Matches orders that are overdue: status is Submitted and SubmittedAt is more than 7 days ago.
/// </summary>
public class OverdueOrderSpecification : Specification<Order>
{
    private readonly DateTime _cutoff;

    public OverdueOrderSpecification(DateTime asOf) => _cutoff = asOf.AddDays(-7);

    // Uses Maybe<T> property access - the MaybeQueryInterceptor rewrites for EF Core
    public override Expression<Func<Order, bool>> ToExpression() =>
#pragma warning disable TRLS006
        order => order.Status == OrderStatus.Submitted
              && order.SubmittedAt.HasValue
              && order.SubmittedAt.Value < _cutoff;
#pragma warning restore TRLS006
}
