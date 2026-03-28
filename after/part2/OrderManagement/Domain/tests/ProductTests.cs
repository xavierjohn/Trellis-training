namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003 // Tests assert success before accessing .Value

public class ProductTests
{
    private static ProductName TestProductName => ProductName.Create("Widget Pro");
    private static Sku TestSku => Sku.Create("WGT-PRO01");
    private static Money TestPrice => Money.Create(9.99m, "USD");

    [Fact]
    public void TryCreate_valid_product_with_zero_initial_stock()
    {
        var result = Product.TryCreate(TestProductName, TestSku, TestPrice);

        result.Should().BeSuccess();
        var product = result.Value;
        product.ProductName.Should().Be(TestProductName);
        product.Sku.Should().Be(TestSku);
        product.UnitPrice.Should().Be(TestPrice);
        product.StockQuantity.Value.Should().Be(0);
    }

    [Fact]
    public void TryCreate_with_zero_price_fails()
    {
        var zeroPrice = Money.Create(0m, "USD");

        var result = Product.TryCreate(TestProductName, TestSku, zeroPrice);

        result.Should().BeFailure();
    }

    [Fact]
    public void AddStock_increases_quantity()
    {
        var product = Product.TryCreate(TestProductName, TestSku, TestPrice).Value;

        var result = product.AddStock(StockQuantity.Create(10));

        result.Should().BeSuccess();
        product.StockQuantity.Value.Should().Be(10);
    }

    [Fact]
    public void ReserveStock_decreases_quantity()
    {
        var product = Product.TryCreate(TestProductName, TestSku, TestPrice).Value;
        product.AddStock(StockQuantity.Create(10)).Should().BeSuccess();

        var result = product.ReserveStock(LineItemQuantity.Create(3));

        result.Should().BeSuccess();
        product.StockQuantity.Value.Should().Be(7);
    }

    [Fact]
    public void ReserveStock_with_insufficient_stock_fails()
    {
        var product = Product.TryCreate(TestProductName, TestSku, TestPrice).Value;
        product.AddStock(StockQuantity.Create(2)).Should().BeSuccess();

        var result = product.ReserveStock(LineItemQuantity.Create(5));

        result.Should().BeFailure();
    }

    [Fact]
    public void ReleaseStock_after_reserve_restores_quantity()
    {
        var product = Product.TryCreate(TestProductName, TestSku, TestPrice).Value;
        product.AddStock(StockQuantity.Create(10)).Should().BeSuccess();
        product.ReserveStock(LineItemQuantity.Create(3)).Should().BeSuccess();

        product.ReleaseStock(LineItemQuantity.Create(3));

        product.StockQuantity.Value.Should().Be(10);
    }
}
