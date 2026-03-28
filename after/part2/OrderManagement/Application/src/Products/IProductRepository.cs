namespace OrderManagement.Application;

using OrderManagement.Domain;

/// <summary>Repository interface for Product persistence.</summary>
public interface IProductRepository
{
    /// <summary>Finds a product by ID. Returns Maybe.None if not found.</summary>
    Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken);

    /// <summary>Saves a new or updated product. Returns ConflictError on duplicate SKU.</summary>
    Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken);
}
