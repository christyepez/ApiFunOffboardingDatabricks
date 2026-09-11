using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class SwaggerController(IConfiguration configuration)
{
    private readonly string _stylesheetUrl = configuration.GetRequiredSection("SwaggerUi:StylesheetUrl").Value!;
    private readonly string _bundleUrl = configuration.GetRequiredSection("SwaggerUi:BundleUrl").Value!;
    private readonly string _contentSecurityPolicy = configuration.GetRequiredSection("SwaggerUi:ContentSecurityPolicy").Value!;

    private const string SwaggerInitializer = """
window.onload = () => {
  SwaggerUIBundle({
    url: window.location.pathname.replace(/\/swagger(?:\/index\.html)?$/, '/openapi.yaml'),
    dom_id: '#swagger-ui',
    deepLinking: true,
    displayRequestDuration: true,
    persistAuthorization: false,
    tryItOutEnabled: true
  });
};
""";

    [Function("SwaggerUi")]
    public Task<HttpResponseData> SwaggerAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger")] HttpRequestData req,
        CancellationToken cancellationToken)
        => CreateSwaggerResponseAsync(req, cancellationToken);

    [Function("SwaggerUiIndex")]
    public Task<HttpResponseData> SwaggerIndexAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/index.html")] HttpRequestData req,
        CancellationToken cancellationToken)
        => CreateSwaggerResponseAsync(req, cancellationToken);

    [Function("SwaggerInitializer")]
    public async Task<HttpResponseData> SwaggerInitializerAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/init.js")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/javascript; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        await response.WriteStringAsync(SwaggerInitializer, cancellationToken);
        return response;
    }

    [Function("OpenApiDocument")]
    public async Task<HttpResponseData> OpenApiAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi.yaml")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
        if (!File.Exists(path))
        {
            var missing = req.CreateResponse(HttpStatusCode.NotFound);
            missing.Headers.Add("Cache-Control", "no-store");
            await missing.WriteStringAsync("OpenAPI document was not found.", cancellationToken);
            return missing;
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/yaml; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteStringAsync(yaml, cancellationToken);
        return response;
    }

    private async Task<HttpResponseData> CreateSwaggerResponseAsync(HttpRequestData req, CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        response.Headers.Add("Content-Security-Policy", _contentSecurityPolicy);
        await response.WriteStringAsync(CreateSwaggerHtml(req), cancellationToken);
        return response;
    }

    private string CreateSwaggerHtml(HttpRequestData req)
    {
        var path = req.Url.AbsolutePath;
        var swaggerBasePath = path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase)
            ? path[..^"/index.html".Length]
            : path.TrimEnd('/');
        var initializerPath = $"{swaggerBasePath}/init.js";

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>IDB Invest Offboarding API - Swagger</title>
  <link rel="stylesheet" href="{{_stylesheetUrl}}" />
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="{{_bundleUrl}}"></script>
  <script src="{{initializerPath}}"></script>
</body>
</html>
""";
    }
}