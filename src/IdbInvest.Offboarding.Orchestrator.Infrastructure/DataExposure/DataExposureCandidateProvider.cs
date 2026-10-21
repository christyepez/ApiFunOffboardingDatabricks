using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Models;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.DataExposure;

public sealed class DataExposureCandidateProvider(
    HttpClient client,
    IDataExposureAccessTokenProvider tokenProvider,
    IOptions<DataExposureOptions> options) : IOffboardingCandidateProvider
{
    private readonly DataExposureOptions _options = options.Value;

    public async Task<OffboardingCandidate?> GetByEmployeeIdAsync(
        string employeeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return null;

        var filter = Uri.EscapeDataString($"employeeId:eq:{employeeId.Trim()}");
        var relativeUrl =
            $"offboarding/v1/employees?fields=employeeId,email,terminationDate&filter={filter}&page=1&pageSize=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrWhiteSpace(_options.SubscriptionKey))
            request.Headers.TryAddWithoutValidation(
                "Ocp-Apim-Subscription-Key",
                _options.SubscriptionKey);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() == 0)
            return null;

        var item = data[0];
        var resolvedEmployeeId = GetRequiredString(item, "employeeId");
        var email = GetRequiredString(item, "email");
        var terminationRaw = GetRequiredString(item, "terminationDate");

        if (!DateOnly.TryParse(
                terminationRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var terminationDate))
            throw new InvalidOperationException(
                $"Data Exposure returned an invalid terminationDate for employee '{resolvedEmployeeId}'.");

        return new OffboardingCandidate(resolvedEmployeeId, email, terminationDate);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrWhiteSpace(_options.SubscriptionKey))
            request.Headers.TryAddWithoutValidation(
                "Ocp-Apim-Subscription-Key",
                _options.SubscriptionKey);

        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
            throw new InvalidOperationException(
                $"Data Exposure response is missing required field '{propertyName}'.");

        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"Data Exposure response field '{propertyName}' is empty.");
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
