namespace Api.Tests;

using System;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit.Sdk;
using Xunit.v3;

public class TestWebApplicationFactoryFixture : WebApplicationFactory<Program>, ITestOutputHelperAccessor
{
    public TestWebApplicationFactoryFixture()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    public ITestOutputHelper? OutputHelper { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(p => p.AddXUnit(this));
    }
}

[CollectionDefinition(Id)]
public class TestWebApplicationFactoryCollectionFixture : ICollectionFixture<TestWebApplicationFactoryFixture>
{
    public const string Id = "Test web application factory fixture collection";
}
