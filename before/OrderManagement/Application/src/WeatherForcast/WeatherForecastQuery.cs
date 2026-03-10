namespace OrderManagement.Application.WeatherForcast;

using OrderManagement.Domain;
using Mediator;

public class WeatherForecastQuery : IQuery<Result<WeatherForecast>>
{
    public ZipCode ZipCode { get; }

    public static Result<WeatherForecastQuery> TryCreate(ZipCode zipCode)
        => new WeatherForecastQuery(zipCode);

    private WeatherForecastQuery(ZipCode zipCode) => ZipCode = zipCode;
}
