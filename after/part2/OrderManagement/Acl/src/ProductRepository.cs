namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of IProductRepository.
/// </summary>
internal class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context) => _context = context;

    public async Task<Maybe<Product>> FindByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        var entity = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return Maybe.From(entity);
    }

    public async Task<List<Product>> GetByIdsAsync(IReadOnlyList<ProductId> ids, CancellationToken cancellationToken) =>
        await _context.Products
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Result<Unit>> SaveAsync(Product product, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(product);
        if (entry.State == EntityState.Detached)
            _context.Products.Add(product);

        return await _context.SaveChangesResultUnitAsync(cancellationToken).ConfigureAwait(false);
    }
}
