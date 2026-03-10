namespace Application.Tests;

using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddMockAntiCorruptionLayer(this IServiceCollection services) => services;
}
