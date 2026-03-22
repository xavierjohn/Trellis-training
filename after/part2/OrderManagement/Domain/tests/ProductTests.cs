namespace Domain.Tests;

public class ProductTests
{
    [Fact]
    public void TryCreate_WithValidData_ReturnsProduct()
    {
        var name = ProductName.Create("Widget");
        var price = Money.Create(9.99m, "USD");
        Sku.TryCreate("WIDGET001").TryGetValue(out var sku);

        var result = Product.TryCreate(name, sku!, price);

        result.Should().BeSuccess()
            .Which.StockQuantity.Should().Be(0);
    }

    [Fact]
    public void AddStock_WithPositiveQuantity_IncreasesStock()
    {
        var product = CreateTestProduct();

        var result = product.AddStock(50);

        result.Should().BeSuccess()
            .Which.StockQuantity.Should().Be(50);
    }

    [Fact]
    public void AddStock_WithNegativeQuantity_ReturnsValidationError()
    {
        var product = CreateTestProduct();

        var result = product.AddStock(-1);

        result.Should().BeFailure()
            .Which.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void ReserveStock_WithSufficientStock_ReservesSuccessfully()
    {
        var product = CreateTestProduct();
        _ = product.AddStock(100);

        var result = product.ReserveStock(30);

        result.Should().BeSuccess()
            .Which.StockQuantity.Should().Be(70);
    }

    [Fact]
    public void ReserveStock_WithInsufficientStock_ReturnsDomainError()
    {
        var product = CreateTestProduct();
        _ = product.AddStock(10);

        var result = product.ReserveStock(50);

        result.Should().BeFailure()
            .Which.Should().BeOfType<DomainError>();
    }

    [Fact]
    public void Sku_WithInvalidFormat_ReturnsValidationError()
    {
        var result = Sku.TryCreate("invalid sku!");
        result.Should().BeFailure()
            .Which.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void Sku_TooShort_ReturnsValidationError()
    {
        var result = Sku.TryCreate("AB");
        result.Should().BeFailure();
    }

    private static Product CreateTestProduct()
    {
        var name = ProductName.Create("Test Product");
        var price = Money.Create(10m, "USD");
        Sku.TryCreate("TESTPROD001").TryGetValue(out var sku);
        Product.TryCreate(name, sku!, price).TryGetValue(out var product);
        return product!;
    }
}
