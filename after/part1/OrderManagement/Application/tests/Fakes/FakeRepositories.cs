namespace Application.Tests.Fakes;

using OrderManagement.Application.Abstractions;
using OrderManagement.Domain;
using Trellis.Primitives;

public class FakeCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<CustomerId, Customer> _store = [];

    public Task<Result<Customer>> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(id, out var c)
            ? (Result<Customer>)c
            : Error.NotFound($"Customer '{id}' not found"));

    public Task<Result<Maybe<Customer>>> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        var customer = _store.Values.FirstOrDefault(c => c.Email == email);
        return Task.FromResult<Result<Maybe<Customer>>>(
            customer is null ? Maybe<Customer>.None : Maybe.From(customer));
    }

    public Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _store[customer.Id] = customer;
        return Task.FromResult(Result.Success());
    }
}

public class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<ProductId, Product> _store = [];

    public Task<Result<Product>> GetByIdAsync(ProductId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(id, out var p)
            ? (Result<Product>)p
            : Error.NotFound($"Product '{id}' not found"));

    public Task<Result<List<Product>>> GetByIdsAsync(IEnumerable<ProductId> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        var products = _store.Values.Where(p => idSet.Contains(p.Id)).ToList();
        return Task.FromResult<Result<List<Product>>>(products);
    }

    public Task<Result<Maybe<Product>>> FindBySkuAsync(Sku sku, CancellationToken cancellationToken = default)
    {
        var product = _store.Values.FirstOrDefault(p => p.Sku == sku);
        return Task.FromResult<Result<Maybe<Product>>>(
            product is null ? Maybe<Product>.None : Maybe.From(product));
    }

    public Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken = default)
    {
        _store[product.Id] = product;
        return Task.FromResult(Result.Success());
    }
}

public class FakeOrderRepository : IOrderRepository
{
    private readonly Dictionary<OrderId, Order> _store = [];

    public Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(id, out var o)
            ? (Result<Order>)o
            : Error.NotFound($"Order '{id}' not found"));

    public Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        _store[order.Id] = order;
        return Task.FromResult(Result.Success());
    }

    public Task<Result<List<Order>>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken = default)
    {
        var orders = _store.Values.Where(o => o.CustomerId == customerId).ToList();
        return Task.FromResult<Result<List<Order>>>(orders);
    }

    public Task<Result<List<Order>>> GetOverdueOrdersAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var orders = _store.Values
            .Where(o => o.Status == OrderStatus.Submitted
                     && o.SubmittedAt.GetValueOrDefault(DateTime.MaxValue) < cutoff)
            .ToList();
        return Task.FromResult<Result<List<Order>>>(orders);
    }
}
