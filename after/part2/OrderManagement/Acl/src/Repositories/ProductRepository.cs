namespace OrderManagement.AntiCorruptionLayer.Repositories;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
using Trellis.EntityFrameworkCore;
using Trellis.Primitives;

public class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    public async Task<Result<Product>> GetByIdAsync(ProductId id, CancellationToken cancellationToken = default) =>
        await dbContext.Products
            .FirstOrDefaultResultAsync(p => p.Id == id, Error.NotFound($"Product '{id}' not found"), cancellationToken);

    public async Task<Result<List<Product>>> GetByIdsAsync(IEnumerable<ProductId> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        var products = await dbContext.Products
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
        return products;
    }

    public async Task<Result<Maybe<Product>>> FindBySkuAsync(Sku sku, CancellationToken cancellationToken = default) =>
        Result.Success(await dbContext.Products
            .FirstOrDefaultMaybeAsync(p => p.Sku == sku, cancellationToken));

    public async Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(product).State == EntityState.Detached)
            dbContext.Products.Add(product);
        else
            dbContext.Products.Update(product);
        return await dbContext.SaveChangesResultUnitAsync(cancellationToken);
    }
}
