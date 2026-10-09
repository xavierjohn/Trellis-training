namespace Api.Tests;

using System.Reflection;
using Microsoft.AspNetCore.Mvc;

public class PaginationRouteTests
{
    [Theory]
    [InlineData(typeof(OrderManagement.Api.v2026_03_26.Controllers.TodosController))]
    [InlineData(typeof(OrderManagement.Api.v2026_12_01.Controllers.TodosController))]
    public void GetOverdue_AllApiVersions_UsesSharedPaginationRouteName(Type controllerType) =>
        controllerType.GetMethods().Single(method => method.Name == "GetOverdue")
            .GetCustomAttributes<HttpGetAttribute>()
            .Should().ContainSingle().Which.Name.Should().Be("Todos_GetOverdue");
}
