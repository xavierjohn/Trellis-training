namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class GetProductByIdHandler(IProductRepository productRepository)
    : IQueryHandler<GetProductByIdQuery, Result<Product>>
{
    public async ValueTask<Result<Product>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken) =>
        await productRepository.GetByIdAsync(query.ProductId, cancellationToken);
}
