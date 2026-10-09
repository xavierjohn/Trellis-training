namespace OrderManagement.Api;

using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Trellis.ServiceLevelIndicators;
using Trellis.Asp;
using Trellis.Asp.ApiVersioning;
using Trellis.Asp.Authorization;
using Trellis.Asp.Idempotency;
using OrderManagement.Domain;
using Trellis.ServiceDefaults;

internal static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IHostEnvironment environment)
    {
        services.ConfigureOpenTelemetry();
        services.ConfigureServiceLevelIndicators();
        services.AddControllers();
        services.AddTrellis(options => options
            .UseAsp(asp => asp.UseVersionedPageUrls())
            .UseScalarValueValidation()
            .UseProblemDetails()
            .UseIdempotency());
        services.AddResourceCollectionName<Customer>("customers");
        services.AddResourceCollectionName<Product>("products");
        services.AddResourceCollectionName<Order>("orders");
        services.AddResourceCollectionName<LineItem>("line-items");
        services.AddInMemoryIdempotencyStore();
        services.AddApiVersioning(options => options.ApiVersionReader = new QueryStringApiVersionReader())
                .AddMvc(options => options.Conventions.Add(new VersionByNamespaceConvention()))
                .AddApiExplorer()
                .AddOpenApi(options =>
                {
                    options.Document.AddScalarTransformers();
                    options.Document.AddOperationTransformer((operation, context, _) =>
                    {
                        if (!context.Description.ActionDescriptor.EndpointMetadata
                            .OfType<HttpGetAttribute>()
                            .Any(attribute => attribute.Name is "Orders_GetOverdue" or "Customers_ListOrders"))
                            return Task.CompletedTask;

                        operation.Parameters ??= [];
                        operation.Parameters.Add(new OpenApiParameter
                        {
                            Name = "cursor",
                            In = ParameterLocation.Query,
                            Description = "Opaque continuation token. Omit for the first page; empty or repeated values are invalid.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                        });
                        operation.Parameters.Add(new OpenApiParameter
                        {
                            Name = "limit",
                            In = ParameterLocation.Query,
                            Description = $"Requested page size; defaults to {PageSize.Default}, capped at {PageSize.Max}.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                        });
                        return Task.CompletedTask;
                    });
                });
        services.AddHealthChecks();

        if (environment.IsDevelopment())
            services.AddDevelopmentActorProvider(options =>
            {
                // Spec §5.5: when no X-Test-Actor header is present, fall back to a
                // default admin actor that holds every OM permission so casual probes
                // (e.g. /openapi inspection or hand-curled scripts) don't see 403.
                options.DefaultActorId = "development-admin";
                options.DefaultPermissions = new HashSet<string>(StringComparer.Ordinal)
                {
                    Permissions.CustomersCreate,
                    Permissions.ProductsCreate,
                    Permissions.ProductsManageStock,
                    Permissions.OrdersCreate,
                    Permissions.OrdersSubmit,
                    Permissions.OrdersApprove,
                    Permissions.OrdersShip,
                    Permissions.OrdersDeliver,
                    Permissions.OrdersCancel,
                    Permissions.OrdersRead,
                    Permissions.OrdersReadAll,
                };
            });
        else
            throw new InvalidOperationException(
                "Production IActorProvider not configured. " +
                "Register AddEntraActorProvider() with your Azure Entra ID configuration for non-development environments.");

        return services;
    }

    private static IServiceCollection ConfigureOpenTelemetry(this IServiceCollection services)
    {
        static void configureResource(ResourceBuilder r) => r.AddService(
            serviceName: "OrderManagement",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

        services.AddOpenTelemetry()
            .ConfigureResource(configureResource)
            .WithMetrics(builder =>
            {
                builder.AddAspNetCoreInstrumentation();
                builder.AddServiceLevelIndicatorInstrumentation();
                builder.AddMeter(
                    "Microsoft.AspNetCore.Hosting",
                    "Microsoft.AspNetCore.Server.Kestrel",
                    "System.Net.Http");
                builder.AddOtlpExporter();
            })
            .WithTracing(builder =>
            {
                builder.AddAspNetCoreInstrumentation();
                builder.AddTrellisPrimitivesInstrumentation();
                builder.AddOtlpExporter();
            });

        return services;
    }

    private static IServiceCollection ConfigureServiceLevelIndicators(this IServiceCollection services)
    {
        services.AddServiceLevelIndicator(options =>
        {
            options.LocationId = ServiceLevelIndicator.CreateLocationId("public", "westus3");
        })
        .AddMvc()
        .AddApiVersion();

        return services;
    }
}
