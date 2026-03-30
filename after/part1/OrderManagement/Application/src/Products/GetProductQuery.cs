namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record GetProductQuery(ProductId ProductId) : IQuery<Result<Product>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsRead];
}

public sealed class GetProductQueryHandler : IQueryHandler<GetProductQuery, Result<Product>>
{
    private readonly IProductRepository _repository;

    public GetProductQueryHandler(IProductRepository repository) => _repository = repository;

    public async ValueTask<Result<Product>> Handle(GetProductQuery query, CancellationToken cancellationToken) =>
        (await _repository.FindByIdAsync(query.ProductId, cancellationToken))
            .ToResult(Error.NotFound($"Product {query.ProductId.Value} not found."));
}
