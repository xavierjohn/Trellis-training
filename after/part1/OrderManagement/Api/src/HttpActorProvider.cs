namespace OrderManagement.Api;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Trellis.Authorization;

public class HttpActorProvider : IActorProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpActorProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public Actor GetCurrentActor()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
            return CreateAdminActor();

        var header = context.Request.Headers["X-Test-Actor"].FirstOrDefault();
        if (string.IsNullOrEmpty(header))
            return CreateAdminActor();

        try
        {
            var actorData = System.Text.Json.JsonSerializer.Deserialize<TestActorData>(header, _jsonOptions);

            if (actorData is null || string.IsNullOrEmpty(actorData.Id))
                return CreateAdminActor();

            return Actor.Create(actorData.Id, (actorData.Permissions ?? []).ToHashSet());
        }
        catch
        {
            return CreateAdminActor();
        }
    }

    private static Actor CreateAdminActor()
    {
        var allPermissions = new HashSet<string>
        {
            Domain.Permissions.CustomersCreate,
            Domain.Permissions.ProductsCreate,
            Domain.Permissions.ProductsManageStock,
            Domain.Permissions.OrdersCreate,
            Domain.Permissions.OrdersSubmit,
            Domain.Permissions.OrdersApprove,
            Domain.Permissions.OrdersShip,
            Domain.Permissions.OrdersDeliver,
            Domain.Permissions.OrdersCancel,
            Domain.Permissions.OrdersRead,
            Domain.Permissions.OrdersReadAll,
        };
        return Actor.Create("admin", allPermissions);
    }

    private sealed record TestActorData(string Id, string[]? Permissions);
}
