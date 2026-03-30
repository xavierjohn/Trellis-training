namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

internal class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context) => _context = context;

    public async Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken) =>
        await _context.Products.FirstOrDefaultMaybeAsync(p => p.Id == id, cancellationToken);

    public async Task<List<Product>> GetByIdsAsync(List<ProductId> ids, CancellationToken cancellationToken) =>
        await _context.Products.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

    public async Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(product);
        if (entry.State == EntityState.Detached)
            _context.Products.Add(product);

        return await _context.SaveChangesResultUnitAsync(cancellationToken);
    }
}
