namespace OrderManagement.Api.v2026_11_12.Models;

using System;
using System.Collections.Generic;
using Trellis.Primitives;

public record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    ShippingAddressDto ShippingAddress);

public record ShippingAddressDto(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);

public record ProductDto(
    Guid Id,
    string ProductName,
    string Sku,
    decimal UnitPriceAmount,
    string UnitPriceCurrency,
    int StockQuantity);

public record OrderDto(
    Guid Id,
    Guid CustomerId,
    string CreatedByActorId,
    string Status,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ShippedAt,
    List<LineItemDto> LineItems);

public record LineItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPriceAmount,
    string UnitPriceCurrency,
    decimal TotalAmount);

public record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);

public record CreateProductRequest(
    string ProductName,
    string Sku,
    decimal UnitPriceAmount,
    string UnitPriceCurrency);

public record AddStockRequest(int Quantity);

public record CreateDraftOrderRequest(
    Guid CustomerId,
    List<AddLineItemRequest> LineItems);

public record AddLineItemRequest(
    Guid ProductId,
    int Quantity);
