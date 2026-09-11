using System.Collections;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

internal sealed class TestFunctionContext : FunctionContext
{
    private readonly TestInvocationFeatures _features = new();

    public TestFunctionContext()
    {
        var services = new ServiceCollection();
        services.AddOptions<WorkerOptions>().Configure(options =>
            options.Serializer = new JsonObjectSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        InstanceServices = services.BuildServiceProvider();
    }

    public override string InvocationId => "test-invocation";
    public override string FunctionId => "test-function";
    public override TraceContext TraceContext => null!;
    public override BindingContext BindingContext => null!;
    public override RetryContext RetryContext => null!;
    public override IServiceProvider InstanceServices { get; set; }
    public override FunctionDefinition FunctionDefinition => null!;
    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
    public override IInvocationFeatures Features => _features;
}

internal sealed class TestInvocationFeatures : IInvocationFeatures
{
    private readonly Dictionary<Type, object> _items = new();
    public void Set<T>(T instance) => _items[typeof(T)] = instance!;
    public T Get<T>() => _items.TryGetValue(typeof(T), out var value) ? (T)value : default!;
    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class TestHttpRequestData : HttpRequestData
{
    private readonly Uri _url;
    private readonly HttpHeadersCollection _headers = new();

    public TestHttpRequestData(string url, TestFunctionContext? context = null) : base(context ?? new TestFunctionContext())
    {
        _url = new Uri(url);
        Body = new MemoryStream();
    }

    public override Stream Body { get; }
    public override HttpHeadersCollection Headers => _headers;
    public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();
    public override Uri Url => _url;
    public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();
    public override string Method => "GET";
    public override HttpResponseData CreateResponse() => new TestHttpResponseData(FunctionContext);
}

internal sealed class TestHttpResponseData : HttpResponseData
{
    public TestHttpResponseData(FunctionContext context) : base(context)
    {
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();
        Cookies = new TestHttpCookies();
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; }
    public override Stream Body { get; set; }
    public override HttpCookies Cookies { get; }

    public string ReadBody()
    {
        Body.Position = 0;
        using var reader = new StreamReader(Body, leaveOpen: true);
        return reader.ReadToEnd();
    }
}

internal sealed class TestHttpCookies : HttpCookies
{
    public override void Append(string name, string value) { }
    public override void Append(IHttpCookie cookie) { }
    public override IHttpCookie CreateNew() => throw new NotSupportedException();
}
