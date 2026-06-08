namespace Application.Tests;

using OrderManagement.Application.Customers;
using OrderManagement.Application.Orders;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing;

/// <summary>In-memory fake adapting <see cref="FakeRepository{T, TId}"/> to <see cref="ICustomerRepository"/>.</summary>
internal sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly FakeRepository<Customer, CustomerId> _repo;
    public FakeCustomerRepository(FakeRepository<Customer, CustomerId> repo) => _repo = repo;

    public Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken) =>
        _repo.FindByIdAsync(id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken) =>
        Task.FromResult(_repo.GetAll().Any(c => c.Email == email));

    public void Add(Customer customer) => _repo.Add(customer);
}

/// <summary>In-memory fake adapting <see cref="FakeRepository{T, TId}"/> to <see cref="IProductRepository"/>.</summary>
internal sealed class FakeProductRepository : IProductRepository
{
    private readonly FakeRepository<Product, ProductId> _repo;
    public FakeProductRepository(FakeRepository<Product, ProductId> repo) => _repo = repo;

    public Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken) =>
        _repo.FindByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Product>> FindManyByIdAsync(IEnumerable<ProductId> ids, CancellationToken cancellationToken)
    {
        var idSet = ids.ToHashSet();
        IReadOnlyList<Product> products = _repo.GetAll().Where(p => idSet.Contains(p.Id)).ToList();
        return Task.FromResult(products);
    }

    public Task<bool> ExistsBySkuAsync(Sku sku, CancellationToken cancellationToken) =>
        Task.FromResult(_repo.GetAll().Any(p => p.Sku == sku));

    public void Add(Product product) => _repo.Add(product);
}

/// <summary>In-memory fake adapting <see cref="FakeRepository{T, TId}"/> to <see cref="IOrderRepository"/>.</summary>
internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly FakeRepository<Order, OrderId> _repo;
    public FakeOrderRepository(FakeRepository<Order, OrderId> repo) => _repo = repo;

    public Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        _repo.FindByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Order>> ListByCustomerAsync(CustomerId customerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Order> result = _repo.GetAll().Where(o => o.CustomerId == customerId).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Order>> QueryAsync(Specification<Order> specification, CancellationToken cancellationToken) =>
        _repo.QueryAsync(specification, cancellationToken);

    public void Add(Order order) => _repo.Add(order);
}

/// <summary>Fake resource loader for the OM ownership-checked Cancel flow.</summary>
internal sealed class FakeOrderResourceLoader : Trellis.Authorization.SharedResourceLoaderById<Order, OrderId>
{
    private readonly IOrderRepository _repository;
    public FakeOrderResourceLoader(IOrderRepository repository) => _repository = repository;

    public override async Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken)
    {
        var maybe = await _repository.FindByIdAsync(id, cancellationToken);
        return maybe.TryGetValue(out var order)
            ? Result.Ok(order)
            : Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Order>(id))
            { Detail = $"Order {id.Value} not found." });
    }
}

