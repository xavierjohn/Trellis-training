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

    public Task<Result<Page<Order>>> ListByCustomerPageAsync(
        CustomerId customerId, PageSize pageSize, Cursor? cursor, CancellationToken cancellationToken) =>
        Task.FromResult(PageInMemory(_repo.GetAll().Where(o => o.CustomerId == customerId), pageSize, cursor));

    public Task<Result<Page<Order>>> QueryPageAsync(
        Specification<Order> specification, PageSize pageSize, Cursor? cursor, CancellationToken cancellationToken) =>
        Task.FromResult(PageInMemory(_repo.GetAll().Where(specification.ToExpression().Compile()), pageSize, cursor));

    public void Add(Order order) => _repo.Add(order);

    // Mirrors Trellis' EF ToPageAsync seek semantics in memory so fake-backed handler tests
    // exercise the same cursor / limit / over-fetch behavior as the SQLite adapter.
    private static Result<Page<Order>> PageInMemory(IEnumerable<Order> source, PageSize pageSize, Cursor? cursor)
    {
        Guid? afterId = null;
        if (cursor is { } c)
        {
            var decoded = CursorCodec.TryDecode<Guid>(c);
            if (!decoded.TryGetValue(out var id, out var error))
                return Result.Fail<Page<Order>>(error);
            afterId = id;
        }

        var overFetched = source
            .OrderBy(o => o.Id.Value)
            .Where(o => afterId is not Guid g || o.Id.Value.CompareTo(g) > 0)
            .Take(pageSize.Applied + 1)
            .ToList();

        return Result.Ok(PageBuilder.FromOverFetch(overFetched, pageSize, o => o.Id.Value));
    }
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

