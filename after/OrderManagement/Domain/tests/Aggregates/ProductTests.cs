namespace Domain.Tests.Aggregates;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public class ProductTests
{
    [Fact]
    public void TryCreate_ValidInput_ReturnsSuccess()
    {
        var result = CreateValidProduct();

        result.Should().BeSuccess();
        var product = result.Value;
        product.ProductName.Value.Should().Be("Widget");
        product.Sku.Value.Should().Be("WDG001");
        product.UnitPrice.Amount.Should().Be(9.99m);
        product.StockQuantity.Value.Should().Be(0);
    }

    [Fact]
    public void TryCreate_ZeroPrice_ReturnsFailure()
    {
        var result = Product.TryCreate(
            ProductName.Create("Widget"),
            Sku.TryCreate("WDG001").Value,
            Money.Create(0m, "USD"));

        result.Should().BeFailure();
    }

    [Fact]
    public void TryCreate_NegativePrice_ThrowsBecauseMoneyRejectsNegative()
    {
        // Money.Create throws on negative amounts — negative price is prevented
        // at the value object level, not at the Product level
        var act = () => Money.Create(-1m, "USD");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddStock_IncreasesQuantity()
    {
        var product = CreateValidProduct().Value;

        var result = product.AddStock(50);

        result.Should().BeSuccess();
        result.Value.StockQuantity.Value.Should().Be(50);
    }

    [Fact]
    public void ReserveStock_SufficientStock_DecreasesQuantity()
    {
        var product = CreateValidProduct().Value;
        product.AddStock(50);

        var result = product.ReserveStock(20);

        result.Should().BeSuccess();
        result.Value.StockQuantity.Value.Should().Be(30);
    }

    [Fact]
    public void ReserveStock_InsufficientStock_ReturnsFailure()
    {
        var product = CreateValidProduct().Value;
        product.AddStock(10);

        var result = product.ReserveStock(20);

        result.Should().BeFailure();
    }

    [Fact]
    public void ReleaseStock_IncreasesQuantity()
    {
        var product = CreateValidProduct().Value;
        product.AddStock(50);
        product.ReserveStock(20);

        var result = product.ReleaseStock(10);

        result.Should().BeSuccess();
        result.Value.StockQuantity.Value.Should().Be(40);
    }

    private static Result<Product> CreateValidProduct() =>
        Product.TryCreate(
            ProductName.Create("Widget"),
            Sku.TryCreate("WDG001").Value,
            Money.Create(9.99m, "USD"));
}
