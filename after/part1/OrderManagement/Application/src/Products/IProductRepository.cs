namespace OrderManagement.Application;

using OrderManagement.Domain;
using Trellis.Primitives;

public interface IProductRepository
{
    Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken);
    Task<List<Product>> GetByIdsAsync(List<ProductId> ids, CancellationToken cancellationToken);
    Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken);
}
