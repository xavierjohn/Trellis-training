namespace OrderManagement.Application.Abstractions;

using Trellis.Primitives;

public interface IProductRepository
{
    Task<Result<Product>> GetByIdAsync(ProductId id, CancellationToken cancellationToken = default);
    Task<Result<List<Product>>> GetByIdsAsync(IEnumerable<ProductId> ids, CancellationToken cancellationToken = default);
    Task<Result<Maybe<Product>>> FindBySkuAsync(Sku sku, CancellationToken cancellationToken = default);
    Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken = default);
}
