namespace Application.Tests;

using OrderManagement.Application.Orders;
using OrderManagement.Application.Products;
using OrderManagement.Domain;

public class CommandValidationTests
{
    [Fact]
    public void Validate_CreateDraftOrder_MissingLineItems_ReturnsValidationFailure()
    {
        var command = new CreateDraftOrderCommand(CustomerId.NewUniqueV7(), null!);

        var validation = new CreateDraftOrderCommandValidator().Validate(command);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.PropertyName == nameof(command.LineItems));
    }

    [Fact]
    public void Validate_AddStock_MissingQuantity_ReturnsValidationFailure()
    {
        var command = new AddStockCommand(ProductId.NewUniqueV7(), null!);

        var validation = new AddStockCommandValidator().Validate(command);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.PropertyName == nameof(command.Quantity));
    }
}
