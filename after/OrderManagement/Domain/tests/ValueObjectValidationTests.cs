namespace Domain.Tests;

using OrderManagement.Domain;

public class ValueObjectValidationTests
{
    [Theory]
    [InlineData("ABC123")]
    [InlineData("WIDGET")]
    [InlineData("XYZ1234567890")]
    public void Sku_AcceptsValidUppercaseAlphaNumeric(string value)
    {
        var result = Sku.TryCreate(value);
        result.IsSuccess.Should().BeTrue();
        result.Unwrap().Value.Should().Be(value);
    }

    [Theory]
    [InlineData("abc")]                  // lowercase
    [InlineData("AB")]                   // too short (< 3)
    [InlineData("ABCDEFGHIJ1234567890X")] // 21 chars (> 20)
    [InlineData("ABC-123")]              // hyphen not allowed
    [InlineData("ABC 123")]              // whitespace not allowed
    public void Sku_RejectsInvalidValues(string value)
    {
        Sku.TryCreate(value).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void UnitPrice_MustBePositive(decimal value)
    {
        UnitPrice.TryCreate(value).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void StockQuantity_AllowsZero()
    {
        StockQuantity.TryCreate(0).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void StockQuantity_RejectsNegative(int value)
    {
        StockQuantity.TryCreate(value).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(-1)]
    public void LineItemQuantity_RejectsOutOfRange(int value)
    {
        LineItemQuantity.TryCreate(value).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(999)]
    public void LineItemQuantity_AcceptsValidRange(int value)
    {
        LineItemQuantity.TryCreate(value).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ShippingAddress_EqualityIsStructural()
    {
        var address1 = new ShippingAddress(
            Street.Create("1 Main St"),
            City.Create("Springfield"),
            StateRegion.Create("IL"),
            PostalCode.Create("62701"),
            Country.Create("USA"));
        var address2 = new ShippingAddress(
            Street.Create("1 Main St"),
            City.Create("Springfield"),
            StateRegion.Create("IL"),
            PostalCode.Create("62701"),
            Country.Create("USA"));
        var address3 = new ShippingAddress(
            Street.Create("2 Main St"),
            City.Create("Springfield"),
            StateRegion.Create("IL"),
            PostalCode.Create("62701"),
            Country.Create("USA"));

        address1.Should().Be(address2);
        address1.Should().NotBe(address3);
    }
}