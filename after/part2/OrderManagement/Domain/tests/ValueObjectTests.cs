namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Testing;

public class ValueObjectTests
{
    [Fact]
    public void Sku_Valid_Succeeds() =>
        Sku.TryCreate("WGT-PRO-001").Should().BeSuccess();

    [Fact]
    public void Sku_TooShort_Fails() =>
        Sku.TryCreate("AB").Should().BeFailure();

    [Fact]
    public void Sku_LeadingHyphen_Fails() =>
        Sku.TryCreate("-ABC").Should().BeFailure();

    [Fact]
    public void Sku_TrailingHyphen_Fails() =>
        Sku.TryCreate("ABC-").Should().BeFailure();

    [Fact]
    public void Sku_Lowercase_Fails() =>
        Sku.TryCreate("abc-123").Should().BeFailure();

    [Fact]
    public void FirstName_Empty_Fails() =>
        FirstName.TryCreate("").Should().BeFailure();

    [Fact]
    public void FirstName_Valid_Succeeds() =>
        FirstName.TryCreate("John").Should().BeSuccess();

    [Fact]
    public void LineItemQuantity_Zero_Fails() =>
        LineItemQuantity.TryCreate(0).Should().BeFailure();

    [Fact]
    public void LineItemQuantity_1000_Fails() =>
        LineItemQuantity.TryCreate(1000).Should().BeFailure();

    [Fact]
    public void LineItemQuantity_1_Succeeds() =>
        LineItemQuantity.TryCreate(1).Should().BeSuccess();

    [Fact]
    public void LineItemQuantity_999_Succeeds() =>
        LineItemQuantity.TryCreate(999).Should().BeSuccess();

    [Fact]
    public void StockQuantity_Negative_Fails() =>
        StockQuantity.TryCreate(-1).Should().BeFailure();

    [Fact]
    public void StockQuantity_Zero_Succeeds() =>
        StockQuantity.TryCreate(0).Should().BeSuccess();
}
