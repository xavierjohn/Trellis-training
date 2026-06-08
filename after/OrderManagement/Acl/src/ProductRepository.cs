namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IProductRepository"/>.</summary>
internal sealed class ProductRepository : RepositoryBase<Product, ProductId>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Product>> FindManyByIdAsync(IEnumerable<ProductId> ids, CancellationToken cancellationToken)
    {
        var idList = ids.Distinct().ToArray();
        return await DbSet.Where(p => idList.Contains(p.Id)).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsBySkuAsync(Sku sku, CancellationToken cancellationToken) =>
        DbSet.AnyAsync(p => p.Sku == sku, cancellationToken);
}