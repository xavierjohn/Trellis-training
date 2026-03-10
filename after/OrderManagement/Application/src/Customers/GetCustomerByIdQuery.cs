namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public sealed record GetCustomerByIdQuery(CustomerId CustomerId) : IQuery<Result<Customer>>;
