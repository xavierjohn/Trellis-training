namespace OrderManagement.Api;

using System.Text.Json;
using OrderManagement.Domain;
using Trellis.Authorization;

public class TestActorProvider(IHttpContextAccessor httpContextAccessor) : IActorProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Actor DefaultAdmin = Actor.Create(
        "admin-default",
        new HashSet<string>
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
            Permissions.OrdersReadAll
        });

    public Actor GetCurrentActor()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return DefaultAdmin;
        }

        var header = httpContext.Request.Headers["X-Test-Actor"].FirstOrDefault();
        if (string.IsNullOrEmpty(header))
        {
            return DefaultAdmin;
        }

        var actorData = JsonSerializer.Deserialize<TestActorData>(header, JsonOptions);

        if (actorData is null)
        {
            return DefaultAdmin;
        }

        return Actor.Create(
            actorData.Id,
            new HashSet<string>(actorData.Permissions ?? []));
    }

    private sealed class TestActorData
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = [];
    }
}
