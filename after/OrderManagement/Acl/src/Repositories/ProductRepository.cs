namespace OrderManagement.AntiCorruptionLayer.Repositories;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.EntityFrameworkCore;

public class ProductRepository(OrderManagementDbContext dbContext) : IProductRepository
{
    public async Task<Result<Product>> GetByIdAsync(ProductId id, CancellationToken ct) =>
        await dbContext.Products
            .FirstOrDefaultResultAsync(p => p.Id == id, Error.NotFound($"Product '{id}' not found."), ct);

    public async Task<Result<List<Product>>> GetByIdsAsync(List<ProductId> ids, CancellationToken ct)
    {
        var products = await dbContext.Products
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

        return Result.Success(products);
    }

    public async Task<Result<Product>> SaveAsync(Product product, CancellationToken ct)
    {
        var entry = dbContext.Entry(product);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Products.Add(product);
        }

        return await dbContext.SaveChangesResultUnitAsync(ct)
            .MapAsync(_ => product);
    }

    public async Task<Result<bool>> ExistsBySkuAsync(Sku sku, CancellationToken ct) =>
        Result.Success(await dbContext.Products.AnyAsync(p => p.Sku == sku, ct));
}
