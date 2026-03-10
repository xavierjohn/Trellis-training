namespace Domain.Tests.ValueObjects;

using OrderManagement.Domain.ValueObjects;

public class StockQuantityTests
{
    [Fact]
    public void TryCreate_Zero_ReturnsSuccess()
    {
        var result = StockQuantity.TryCreate(0);

        result.Should().BeSuccess()
            .Which.Value.Should().Be(0);
    }

    [Fact]
    public void TryCreate_PositiveValue_ReturnsSuccess()
    {
        var result = StockQuantity.TryCreate(100);

        result.Should().BeSuccess()
            .Which.Value.Should().Be(100);
    }

    [Fact]
    public void TryCreate_NegativeValue_ReturnsFailure()
    {
        var result = StockQuantity.TryCreate(-1);

        result.Should().BeFailure();
    }

    [Fact]
    public void Add_PositiveQuantity_IncreasesStock()
    {
        var stock = StockQuantity.TryCreate(10).Value;

        var result = stock.Add(5);

        result.Should().BeSuccess()
            .Which.Value.Should().Be(15);
    }

    [Fact]
    public void Add_ZeroOrNegative_ReturnsFailure()
    {
        var stock = StockQuantity.TryCreate(10).Value;

        stock.Add(0).Should().BeFailure();
        stock.Add(-1).Should().BeFailure();
    }

    [Fact]
    public void Reserve_SufficientStock_DecreasesStock()
    {
        var stock = StockQuantity.TryCreate(10).Value;

        var result = stock.Reserve(5);

        result.Should().BeSuccess()
            .Which.Value.Should().Be(5);
    }

    [Fact]
    public void Reserve_InsufficientStock_ReturnsFailure()
    {
        var stock = StockQuantity.TryCreate(5).Value;

        var result = stock.Reserve(10);

        result.Should().BeFailure();
    }

    [Fact]
    public void Reserve_ZeroOrNegative_ReturnsFailure()
    {
        var stock = StockQuantity.TryCreate(10).Value;

        stock.Reserve(0).Should().BeFailure();
        stock.Reserve(-1).Should().BeFailure();
    }

    [Fact]
    public void Release_PositiveQuantity_IncreasesStock()
    {
        var stock = StockQuantity.TryCreate(10).Value;

        var result = stock.Release(5);

        result.Should().BeSuccess()
            .Which.Value.Should().Be(15);
    }

    [Fact]
    public void Release_ZeroOrNegative_ReturnsFailure()
    {
        var stock = StockQuantity.TryCreate(10).Value;

        stock.Release(0).Should().BeFailure();
        stock.Release(-1).Should().BeFailure();
    }

    [Fact]
    public void Zero_ReturnsZeroQuantity()
    {
        StockQuantity.Zero.Value.Should().Be(0);
    }
}
