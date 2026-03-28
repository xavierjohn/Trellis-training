namespace Api.Tests._2026_11_12;

/// <summary>Test DTO matching CustomerResponse from API.</summary>
public record CustomerResponse
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string? PhoneNumber { get; init; }
    public ShippingAddressDto ShippingAddress { get; init; } = null!;
}

/// <summary>Test DTO matching ProductResponse from API.</summary>
public record ProductResponse
{
    public Guid Id { get; init; }
    public string ProductName { get; init; } = null!;
    public string Sku { get; init; } = null!;
    public MoneyDto UnitPrice { get; init; } = null!;
    public int StockQuantity { get; init; }
}

/// <summary>Test DTO matching OrderResponse from API.</summary>
public record OrderResponse
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string CreatedByActorId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public MoneyDto Total { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    public LineItemResponse[] LineItems { get; init; } = [];
}

/// <summary>Test DTO matching LineItemResponse from API.</summary>
public record LineItemResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = null!;
    public int Quantity { get; init; }
    public MoneyDto UnitPrice { get; init; } = null!;
}

/// <summary>Test DTO matching MoneyDto from API.</summary>
public record MoneyDto
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
}

/// <summary>Test DTO matching ShippingAddressDto from API.</summary>
public record ShippingAddressDto
{
    public string Street { get; init; } = null!;
    public string City { get; init; } = null!;
    public string State { get; init; } = null!;
    public string PostalCode { get; init; } = null!;
    public string Country { get; init; } = null!;
}
