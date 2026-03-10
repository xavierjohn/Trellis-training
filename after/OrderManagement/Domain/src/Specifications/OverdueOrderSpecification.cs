namespace OrderManagement.Domain.Specifications;

using System.Linq.Expressions;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public class OverdueOrderSpecification : Specification<Order>
{
    private readonly DateTime _cutoffDate;

    public OverdueOrderSpecification(DateTime utcNow)
    {
        _cutoffDate = utcNow.AddDays(-7);
    }

    public override Expression<Func<Order, bool>> ToExpression()
    {
        // Note: This specification is used for in-memory evaluation via IsSatisfiedBy.
        // For EF Core queries, the repository uses direct LINQ with the backing field.
        return order => order.Status == OrderStatus.Submitted
            && order.SubmittedAt.HasValue
            && order.SubmittedAt.GetValueOrDefault(DateTime.MaxValue) <= _cutoffDate;
    }
}
