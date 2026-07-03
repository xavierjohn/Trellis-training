namespace Api.Tests;

using System.IO;
using Trellis.Testing.AspNetCore.Http;

/// <summary>
/// Replays the checked-in <c>api.http</c> against the test host and asserts every request meets
/// its <c># @expect</c> contract (status + required headers). This guards <c>api.http</c> from
/// drifting out of sync with the API — including the cursor pagination flow, which pages the
/// customer's orders with a small limit and then follows the emitted <c>next</c> cursor.
/// </summary>
[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class ApiHttpReplayTests
{
    private const string ApiVersion = "2026-11-12";
    private const string AdminActor =
        "{\"Id\":\"admin-1\",\"Permissions\":[\"customers:create\",\"products:create\",\"products:manage-stock\",\"orders:create\",\"orders:submit\",\"orders:approve\",\"orders:ship\",\"orders:deliver\",\"orders:cancel\",\"orders:read\",\"orders:read-all\"]}";
    private const string UserActor =
        "{\"Id\":\"user-1\",\"Permissions\":[\"orders:read\"]}";

    private readonly TestWebApplicationFactoryFixture _fixture;

    public ApiHttpReplayTests(TestWebApplicationFactoryFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ApiHttp_ReplaysGreen()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var httpFile = Path.Combine(AppContext.BaseDirectory, "api.http");
        var vars = new Dictionary<string, string>
        {
            ["host"] = client.BaseAddress!.ToString().TrimEnd('/'),
            ["apiVersion"] = ApiVersion,
            ["adminActor"] = AdminActor,
            ["userActor"] = UserActor,
        };

        var requests = HttpFileParser.ParseFile(httpFile, vars);
        var results = await HttpFileRunner.RunAsync(client, requests, ct);

        results.Should().NotBeEmpty();
        foreach (var result in results)
            HttpFileAssertions.AssertExpectationsMet(result);
    }
}
