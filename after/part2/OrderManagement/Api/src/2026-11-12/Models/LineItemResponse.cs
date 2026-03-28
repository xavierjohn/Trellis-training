namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Response model for a single order line item.
/// </summary>
public record LineItemResponse
{
    /// <summary>Unique line item identifier.</summary>
    public Guid Id { get; init; }
    /// <summary>Product identifier.</summary>
    public Guid ProductId { get; init; }
    /// <summary>Snapshot of the product name at the time the line item was added.</summary>
    public string ProductName { get; init; } = null!;
    /// <summary>Quantity ordered.</summary>
    public int Quantity { get; init; }
    /// <summary>Snapshot of the unit price at the time the line item was added.</summary>
    public MoneyDto UnitPrice { get; init; } = null!;

    /// <summary>Maps from domain entity.</summary>
    public static LineItemResponse From(LineItem li) => new()
    {
        Id = li.Id.Value,
        ProductId = li.ProductId.Value,
        ProductName = li.ProductName.Value,
        Quantity = li.Quantity.Value,
        UnitPrice = MoneyDto.From(li.UnitPrice)
    };
}
