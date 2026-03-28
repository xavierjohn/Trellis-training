namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Gets a product by ID.</summary>
public sealed record GetProductByIdQuery(ProductId ProductId) : IQuery<Result<Product>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsRead];
}

/// <summary>Handler for <see cref="GetProductByIdQuery"/>.</summary>
public sealed class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, Result<Product>>
{
    private readonly IProductRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetProductByIdQueryHandler"/>.</summary>
    public GetProductByIdQueryHandler(IProductRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async ValueTask<Result<Product>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken) =>
        (await _repository.FindByIdAsync(query.ProductId, cancellationToken))
            .ToResult(Error.NotFound($"Product {query.ProductId} not found."));
}
