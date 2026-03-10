namespace Domain.Tests.ValueObjects;

using OrderManagement.Domain.ValueObjects;

public class LineItemQuantityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(999)]
    public void TryCreate_ValidQuantity_ReturnsSuccess(int value)
    {
        var result = LineItemQuantity.TryCreate(value);

        result.Should().BeSuccess()
            .Which.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryCreate_ZeroOrNegative_ReturnsFailure(int value)
    {
        var result = LineItemQuantity.TryCreate(value);

        result.Should().BeFailure();
    }

    [Fact]
    public void TryCreate_Over999_ReturnsFailure()
    {
        var result = LineItemQuantity.TryCreate(1000);

        result.Should().BeFailure();
    }
}
