namespace Application.Tests;

using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Testing.Fakes;

internal class FakeProductRepositoryAdapter : IProductRepository
{
    private readonly FakeRepository<Product, ProductId> _repo;

    public FakeProductRepositoryAdapter(FakeRepository<Product, ProductId> repo)
    {
        _repo = repo;
        _repo.WithUniqueConstraint(p => p.Sku);
    }

    public async Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        var result = await _repo.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Maybe.From(result.Value) : Maybe<Product>.None;
    }

    public Task<List<Product>> GetByIdsAsync(IReadOnlyList<ProductId> ids, CancellationToken cancellationToken)
    {
        var products = _repo.GetAll().Where(p => ids.Contains(p.Id)).ToList();
        return Task.FromResult(products);
    }

    public async Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken)
    {
        var result = await _repo.SaveAsync(product, cancellationToken);
        return result.Map(_ => default(Unit));
    }
}
