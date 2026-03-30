namespace Domain.Tests;

using OrderManagement.Domain;

public class SkuTests
{
    [Theory]
    [InlineData("WGT-001")]
    [InlineData("ABC123")]
    [InlineData("X-1")]
    [InlineData("WGT-PRO-001")]
    public void TryCreate_valid_sku_succeeds(string value)
    {
        var result = Sku.TryCreate(value);
        result.Should().BeSuccess();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("-WGT")]
    [InlineData("WGT-")]
    [InlineData("wgt-001")]
    public void TryCreate_invalid_sku_fails(string? value)
    {
        var result = Sku.TryCreate(value);
        result.Should().BeFailure();
    }
}
