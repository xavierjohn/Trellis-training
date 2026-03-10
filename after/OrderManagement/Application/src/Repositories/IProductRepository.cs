namespace OrderManagement.Application.Repositories;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public interface IProductRepository
{
    Task<Result<Product>> GetByIdAsync(ProductId id, CancellationToken ct);
    Task<Result<List<Product>>> GetByIdsAsync(List<ProductId> ids, CancellationToken ct);
    Task<Result<Product>> SaveAsync(Product product, CancellationToken ct);
    Task<Result<bool>> ExistsBySkuAsync(Sku sku, CancellationToken ct);
}
