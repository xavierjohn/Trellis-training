namespace Domain.Tests.ValueObjects;

using OrderManagement.Domain.ValueObjects;

public class SkuTests
{
    [Theory]
    [InlineData("ABC123")]
    [InlineData("SKU")]
    [InlineData("ABCDEFGHIJKLMNOPQRST")]
    public void TryCreate_ValidSku_ReturnsSuccess(string value)
    {
        var result = Sku.TryCreate(value);

        result.Should().BeSuccess();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void TryCreate_EmptyOrNull_ReturnsFailure(string? value)
    {
        var result = Sku.TryCreate(value);

        result.Should().BeFailure();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("A")]
    public void TryCreate_TooShort_ReturnsFailure(string value)
    {
        var result = Sku.TryCreate(value);

        result.Should().BeFailure();
    }

    [Fact]
    public void TryCreate_TooLong_ReturnsFailure()
    {
        var result = Sku.TryCreate(new string('A', 21));

        result.Should().BeFailure();
    }

    [Theory]
    [InlineData("ABC-123")]
    [InlineData("abc 123")]
    public void TryCreate_InvalidCharacters_ReturnsFailure(string value)
    {
        var result = Sku.TryCreate(value);

        result.Should().BeFailure();
    }

    [Fact]
    public void TryCreate_LowercaseIsUppercased()
    {
        var result = Sku.TryCreate("abc123");

        result.Should().BeSuccess()
            .Which.Value.Should().Be("ABC123");
    }
}
