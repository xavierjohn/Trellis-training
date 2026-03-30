#pragma warning disable TRLS003
namespace Application.Tests;

using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing.Fakes;

public sealed class FakeProductRepository : IProductRepository
{
    private readonly FakeRepository<Product, ProductId> _inner = new();

    public async Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        var result = await _inner.FindByIdAsync(id, cancellationToken);
        return result.Value;
    }

    public Task<List<Product>> GetByIdsAsync(List<ProductId> ids, CancellationToken cancellationToken)
    {
        var results = ids
            .Select(id => _inner.Get(id))
            .Where(p => p is not null)
            .Cast<Product>()
            .ToList();
        return Task.FromResult(results);
    }

    public Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken) =>
        _inner.SaveAsync(product, cancellationToken);
}
