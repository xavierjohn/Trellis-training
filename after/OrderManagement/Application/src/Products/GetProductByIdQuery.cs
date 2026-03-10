namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public sealed record GetProductByIdQuery(ProductId ProductId) : IQuery<Result<Product>>;
