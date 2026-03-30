namespace Api.Tests;

[CollectionDefinition(Name)]
public class WebApplicationFixtureCollection : ICollectionFixture<TestWebApplicationFactoryFixture>
{
    public const string Name = "WebApplication";
}
