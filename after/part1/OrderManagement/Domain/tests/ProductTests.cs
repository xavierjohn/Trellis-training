#pragma warning disable TRLS001, TRLS003

namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing;

public class ProductTests
{
    private static ProductName ValidName => ProductName.Create("Widget Pro");
    private static Sku ValidSku => Sku.Create("WGT-PRO-001");
    private static Money ValidPrice => Money.Create(29.99m, "USD");

    [Fact]
    public void TryCreate_Valid_ReturnsSuccess()
    {
        var result = Product.TryCreate(ValidName, ValidSku, ValidPrice);

        result.Should().BeSuccess();
        result.Value.ProductName.Should().Be(ValidName);
        result.Value.Sku.Should().Be(ValidSku);
        result.Value.UnitPrice.Should().Be(ValidPrice);
        result.Value.StockQuantity.Value.Should().Be(0);
    }

    [Fact]
    public void AddStock_PositiveQuantity_IncreasesStock()
    {
        var product = Product.TryCreate(ValidName, ValidSku, ValidPrice).Value;
        var result = product.AddStock(StockQuantity.Create(10));

        result.Should().BeSuccess();
        result.Value.StockQuantity.Value.Should().Be(10);
    }

    [Fact]
    public void ReserveStock_SufficientStock_DecreasesStock()
    {
        var product = Product.TryCreate(ValidName, ValidSku, ValidPrice).Value;
        product.AddStock(StockQuantity.Create(10));

        var result = product.ReserveStock(LineItemQuantity.Create(5));

        result.Should().BeSuccess();
        result.Value.StockQuantity.Value.Should().Be(5);
    }

    [Fact]
    public void ReserveStock_InsufficientStock_ReturnsFailure()
    {
        var product = Product.TryCreate(ValidName, ValidSku, ValidPrice).Value;
        product.AddStock(StockQuantity.Create(3));

        var result = product.ReserveStock(LineItemQuantity.Create(5));

        result.Should().BeFailure();
    }
}
