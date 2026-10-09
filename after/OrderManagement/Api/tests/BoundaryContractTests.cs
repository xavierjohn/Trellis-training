namespace Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;

[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class BoundaryContractTests(TestWebApplicationFactoryFixture fixture)
{
    [Fact]
    public async Task CreateOrder_NullLineItems_Returns422()
    {
        var response = await fixture.CreateClient().PostAsJsonAsync(
            "/api/orders?api-version=2026-11-12",
            new { CustomerId = Guid.NewGuid(), LineItems = (object?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task OpenApi_ListOperations_DeclareRawPaginationParameters()
    {
        var group = fixture.Services.GetRequiredService<IApiVersionDescriptionProvider>()
            .ApiVersionDescriptions.Single().GroupName;
        var response = await fixture.CreateClient().GetAsync(
            $"/openapi/{group}.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        foreach (var route in new[] { "/api/orders/overdue", "/api/customers/{id}/orders" })
        {
            var path = json.RootElement.GetProperty("paths").EnumerateObject()
                .Single(p => string.Equals(p.Name, route, StringComparison.OrdinalIgnoreCase)).Value;
            var parameters = path.GetProperty("get").GetProperty("parameters").EnumerateArray().ToArray();
            parameters.Should().Contain(p => p.GetProperty("name").GetString() == "cursor"
                && p.GetProperty("in").GetString() == "query");
            parameters.Should().Contain(p => p.GetProperty("name").GetString() == "limit"
                && p.GetProperty("in").GetString() == "query");
        }
    }
}
