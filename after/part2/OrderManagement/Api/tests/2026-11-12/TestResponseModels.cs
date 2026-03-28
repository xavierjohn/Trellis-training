namespace Api.Tests._2026_11_12;

/// <summary>
/// Response model for deserialization in tests.
/// </summary>
public class CustomerResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public ShippingAddressResponse? ShippingAddress { get; set; }
}

public class ShippingAddressResponse
{
    public string Street { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string Country { get; set; } = null!;
}

public class ProductResponse
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = null!;
    public string Sku { get; set; } = null!;
    public MoneyResponse? UnitPrice { get; set; }
    public int StockQuantity { get; set; }
}

public class MoneyResponse
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
}

public class OrderResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CreatedByActorId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public MoneyResponse? Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public List<LineItemResponse> LineItems { get; set; } = [];
}

public class LineItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public int Quantity { get; set; }
    public MoneyResponse? UnitPrice { get; set; }
}
