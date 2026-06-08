namespace OrderManagement.Application.Products;

using OrderManagement.Domain;

/// <summary>
/// Repository interface for <see cref="Product"/> persistence.
/// </summary>
public interface IProductRepository
{
    /// <summary>Finds a product by id. Returns <see cref="Maybe{T}.None"/> if not found.</summary>
    Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a batch of products by id. The result has length &lt;= <paramref name="ids"/> and
    /// preserves no particular order; the caller compares by id to detect missing products.
    /// </summary>
    Task<IReadOnlyList<Product>> FindManyByIdAsync(IEnumerable<ProductId> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Returns <c>true</c> when a product with the given SKU already exists.
    /// Used to surface a deterministic 409 Conflict before EF's unique index would otherwise
    /// raise a wrapped <c>DbUpdateException</c> at <c>SaveChangesAsync</c> time.
    /// </summary>
    Task<bool> ExistsBySkuAsync(Sku sku, CancellationToken cancellationToken);

    /// <summary>Stages an aggregate for insertion. The unit-of-work commits on handler success.</summary>
    void Add(Product product);
}
