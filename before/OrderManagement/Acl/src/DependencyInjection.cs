namespace OrderManagement.AntiCorruptionLayer;

using OrderManagement.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddAntiCorruptionLayer(this IServiceCollection services)
        => services.AddSingleton<IWeatherForecastService, WeatherForecastService>();
}
