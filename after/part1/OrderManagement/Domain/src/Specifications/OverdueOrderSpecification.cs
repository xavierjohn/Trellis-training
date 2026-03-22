namespace OrderManagement.Domain.Specifications;

using System.Linq.Expressions;

public class OverdueOrderSpecification : Specification<Order>
{
    private readonly DateTime _cutoff;

    public OverdueOrderSpecification()
    {
        _cutoff = DateTime.UtcNow.AddDays(-7);
    }

    // Must not cache since _cutoff captures mutable state (current time)
    protected override bool CacheCompilation => false;

    public override Expression<Func<Order, bool>> ToExpression() =>
        order => order.Status == OrderStatus.Submitted
              && order.SubmittedAt.GetValueOrDefault(DateTime.MaxValue) < _cutoff;
}
