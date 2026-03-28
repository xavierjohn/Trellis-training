namespace OrderManagement.Application.Products;

using OrderManagement.Domain;

/// <summary>
/// Repository interface for Product persistence.
/// </summary>
public interface IProductRepository
{
    Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken);
    Task<List<Product>> GetByIdsAsync(IReadOnlyList<ProductId> ids, CancellationToken cancellationToken);
    Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken);
}
