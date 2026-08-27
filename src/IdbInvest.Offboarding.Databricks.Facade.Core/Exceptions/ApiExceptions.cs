namespace IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;

public abstract class ApiException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class InvalidQueryException(string message)
    : ApiException("INVALID_QUERY", message, 400);

public sealed class ResourceNotFoundException(string resource)
    : ApiException("RESOURCE_NOT_FOUND", $"Resource '{resource}' was not found or is not exposed.", 404);

public sealed class DependencyUnavailableException(string message)
    : ApiException("DEPENDENCY_UNAVAILABLE", message, 503);
