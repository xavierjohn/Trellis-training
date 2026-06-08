namespace OrderManagement.Domain;

/// <summary>
/// An order is overdue when it has been in <see cref="OrderStatus.Submitted"/> status for
/// more than 7 days without being Approved. Used by the
/// <c>ListOverdueOrdersQuery</c> read path.
/// </summary>
public sealed class OverdueOrderSpecification : Specification<Order>
{
    /// <summary>Threshold in days after which a Submitted order is overdue.</summary>
    public const int OverdueThresholdDays = 7;

    private readonly DateTimeOffset _asOfUtc;

    public OverdueOrderSpecification(DateTimeOffset asOfUtc)
    {
        _asOfUtc = asOfUtc;
    }

    public override System.Linq.Expressions.Expression<Func<Order, bool>> ToExpression()
    {
        var cutoff = _asOfUtc.AddDays(-OverdueThresholdDays);
        return order =>
            order.Status == OrderStatus.Submitted
            && order.SubmittedAt.HasValue
            && order.SubmittedAt.Value < cutoff;
    }
}
