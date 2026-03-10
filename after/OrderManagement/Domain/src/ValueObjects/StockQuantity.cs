namespace OrderManagement.Domain.ValueObjects;

public partial class StockQuantity : ScalarValueObject<StockQuantity, int>, IScalarValue<StockQuantity, int>
{
    private StockQuantity(int value) : base(value) { }

    public static Result<StockQuantity> TryCreate(int value, string? fieldName = null)
    {
        fieldName ??= "StockQuantity";

        if (value < 0)
        {
            return Error.Validation($"{fieldName} cannot be negative.", fieldName);
        }

        return new StockQuantity(value);
    }

    public Result<StockQuantity> Add(int quantity)
    {
        if (quantity <= 0)
        {
            return Error.Validation("Quantity to add must be positive.", "quantity");
        }

        return new StockQuantity(Value + quantity);
    }

    public Result<StockQuantity> Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            return Error.Validation("Quantity to reserve must be positive.", "quantity");
        }

        if (Value < quantity)
        {
            return Error.Validation($"Insufficient stock. Available: {Value}, requested: {quantity}.", "quantity");
        }

        return new StockQuantity(Value - quantity);
    }

    public Result<StockQuantity> Release(int quantity)
    {
        if (quantity <= 0)
        {
            return Error.Validation("Quantity to release must be positive.", "quantity");
        }

        return new StockQuantity(Value + quantity);
    }

    public static StockQuantity Zero => new(0);
}
