namespace Application.Tests;

using Microsoft.Extensions.Hosting;
using OrderManagement.Application;

public class Startup
{
    public static void ConfigureHost(IHostBuilder hostBuilder) =>
        hostBuilder
            .ConfigureServices((context, services) =>
            {
                services.AddApplication()
                        .AddMockDependencies();
            });
}
