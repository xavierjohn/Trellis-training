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
        CustomerId customerId, PageRequest pagination, CancellationToken cancellationToken) =>
        Task.FromResult(PageInMemory(_repo.GetAll().Where(o => o.CustomerId == customerId), pagination));

    public Task<Result<Page<Order>>> QueryPageAsync(
        Specification<Order> specification, PageRequest pagination, CancellationToken cancellationToken) =>
        Task.FromResult(PageInMemory(_repo.GetAll().Where(specification.ToExpression().Compile()), pagination));

    public void Add(Order order) => _repo.Add(order);

    // Mirrors Trellis' EF ToPageAsync seek semantics in memory so fake-backed handler tests
    // exercise the same cursor / limit / over-fetch behavior as the SQLite adapter.
    private static Result<Page<Order>> PageInMemory(IEnumerable<Order> source, PageRequest pagination)
    {
        var codec = CursorCodec.Scalar<Guid>();
        return pagination.Decode(codec).Map(boundary =>
        {
            var overFetched = source
                .OrderBy(o => o.Id.Value)
                .Where(o => boundary.Match(id => o.Id.Value.CompareTo(id) > 0, static () => true))
                .Take(pagination.Size.Applied + 1)
                .ToList();
            return PageBuilder.FromOverFetch(overFetched, pagination.Size, o => codec.Encode(o.Id.Value));
        });
    }
}

/// <summary>Fake resource loader for the OM ownership-checked Cancel flow.</summary>
internal sealed class FakeOrderResourceLoader : Trellis.Authorization.SharedResourceLoaderById<Order, OrderId>
{
    private readonly IOrderRepository _repository;
    public FakeOrderResourceLoader(IOrderRepository repository) => _repository = repository;

    public override Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        _repository.FindByIdAsync(id, cancellationToken)
            .ToResultAsync(() => Error.NotFound.For<Order>(id: id, detail: $"Order {id.Value} not found."));
}
