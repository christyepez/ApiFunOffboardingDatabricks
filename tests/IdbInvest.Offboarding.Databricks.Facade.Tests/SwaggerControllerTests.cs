using System.Net;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;
using Microsoft.Extensions.Configuration;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class SwaggerControllerTests
{
    [Fact]
    public async Task Swagger_ReturnsInteractiveHtml()
    {
        var sut = CreateController();
        var request = new TestHttpRequestData("https://localhost/api/swagger");

        var response = (TestHttpResponseData)await sut.SwaggerAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Headers.GetValues("Content-Type").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/swagger/init.js", response.ReadBody(), StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.GetValues("Cache-Control").Single());
    }

    [Fact]
    public async Task SwaggerIndex_ReturnsInteractiveHtml()
    {
        var sut = CreateController();
        var request = new TestHttpRequestData("https://localhost/api/swagger/index.html");

        var response = (TestHttpResponseData)await sut.SwaggerIndexAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/swagger/init.js", response.ReadBody(), StringComparison.Ordinal);
    }


    [Fact]
    public async Task SwaggerInitializer_ReturnsExternalInitializationScript()
    {
        var sut = CreateController();
        var request = new TestHttpRequestData("https://localhost/api/swagger/init.js");

        var response = (TestHttpResponseData)await sut.SwaggerInitializerAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/javascript", response.Headers.GetValues("Content-Type").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SwaggerUIBundle", response.ReadBody(), StringComparison.Ordinal);
        Assert.Contains("/openapi.yaml", response.ReadBody(), StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", response.ReadBody(), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task OpenApi_ReturnsYamlDocument()
    {
        var target = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
        var original = File.Exists(target) ? await File.ReadAllTextAsync(target) : null;
        try
        {
            await File.WriteAllTextAsync(target, "openapi: 3.0.3\ninfo:\n  title: Test API\n  version: 1.0.0\npaths: {}\n");
            var sut = CreateController();
            var request = new TestHttpRequestData("https://localhost/api/openapi.yaml");

            var response = (TestHttpResponseData)await sut.OpenApiAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("application/yaml", response.Headers.GetValues("Content-Type").Single(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("openapi: 3.0.3", response.ReadBody(), StringComparison.Ordinal);
            Assert.Equal("no-store", response.Headers.GetValues("Cache-Control").Single());
        }
        finally
        {
            if (original is null) File.Delete(target);
            else await File.WriteAllTextAsync(target, original);
        }
    }

    [Fact]
    public async Task OpenApi_Returns404_WhenDocumentIsMissing()
    {
        var target = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
        var backup = target + ".test-backup";
        if (File.Exists(backup)) File.Delete(backup);
        if (File.Exists(target)) File.Move(target, backup);
        try
        {
            var sut = CreateController();
            var request = new TestHttpRequestData("https://localhost/api/openapi.yaml");

            var response = (TestHttpResponseData)await sut.OpenApiAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("not found", response.ReadBody(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(backup)) File.Move(backup, target);
        }
    }
    private static SwaggerController CreateController()
    {
        var settings = new Dictionary<string, string?>
        {
            ["SwaggerUi:StylesheetUrl"] = "/swagger-ui.css",
            ["SwaggerUi:BundleUrl"] = "/swagger-ui-bundle.js",
            ["SwaggerUi:ContentSecurityPolicy"] = "default-src 'none'; script-src 'self'; style-src 'self'; img-src data:; connect-src 'self'; font-src 'self' data:"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        return new SwaggerController(configuration);
    }
}
