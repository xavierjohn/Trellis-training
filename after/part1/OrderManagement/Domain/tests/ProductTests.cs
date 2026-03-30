namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003

public class ProductTests
{
    private static Product CreateProduct(int stockQuantity = 0)
    {
        var product = Product.TryCreate(
            ProductName.Create("Widget"),
            Sku.Create("WGT-001"),
            Money.Create(19.99m, "USD")).Value;

        if (stockQuantity > 0)
            product.AddStock(StockQuantity.Create(stockQuantity)).Should().BeSuccess();

        return product;
    }

    [Fact]
    public void TryCreate_valid_product_succeeds()
    {
        var result = Product.TryCreate(
            ProductName.Create("Widget"),
            Sku.Create("WGT-001"),
            Money.Create(19.99m, "USD"));

        result.Should().BeSuccess();
        var product = result.Value;
        product.ProductName.Value.Should().Be("Widget");
        product.Sku.Value.Should().Be("WGT-001");
        product.StockQuantity.Value.Should().Be(0);
    }

    [Fact]
    public void TryCreate_zero_price_fails()
    {
        var result = Product.TryCreate(
            ProductName.Create("Widget"),
            Sku.Create("WGT-001"),
            Money.Create(0m, "USD"));

        result.Should().BeFailure();
    }

    [Fact]
    public void AddStock_positive_quantity_succeeds()
    {
        var product = CreateProduct();
        var result = product.AddStock(StockQuantity.Create(10));

        result.Should().BeSuccess();
        product.StockQuantity.Value.Should().Be(10);
    }

    [Fact]
    public void ReserveStock_sufficient_succeeds()
    {
        var product = CreateProduct(20);
        var result = product.ReserveStock(StockQuantity.Create(5));

        result.Should().BeSuccess();
        product.StockQuantity.Value.Should().Be(15);
    }

    [Fact]
    public void ReserveStock_insufficient_fails()
    {
        var product = CreateProduct(3);
        var result = product.ReserveStock(StockQuantity.Create(5));

        result.Should().BeFailure()
            .Which.Should().BeOfType<ValidationError>();
    }
}
