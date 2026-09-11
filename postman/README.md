# Postman - Offboarding Data Exposure API

## Files

- `IDBInvest_Offboarding_Postman_Collection.json` - functional and contract test collection.
- `local.postman_environment.json` - local Azure Functions environment.
- `dev-apim.postman_environment.json` - DEV API Management environment template.

## Local execution

1. Import the collection.
2. Import `local.postman_environment.json`.
3. Select `Offboarding Databricks Facade - Local`.
4. Start with `Health / Liveness`.
5. Run `Metadata / Employee metadata contract`.
6. Run the Dynamic Query requests.

Local base URL:

```text
http://localhost:7071/api
```

## DEV through APIM

1. Import `dev-apim.postman_environment.json`.
2. Select `Offboarding Databricks Facade - DEV APIM`.
3. Replace:

```text
baseUrl = https://<apim-host>
```

with the confirmed DEV APIM gateway base URL.
4. Put the Microsoft Entra ID token in:

```text
accessToken
```

5. Enable the bearer header for requests protected by APIM if it is disabled in the collection.

Do not store long-lived tokens or secrets in source control.

## Key variables

| Variable | Default | Purpose |
|---|---|---|
| `resource` | `employees` | Logical resource. |
| `fields` | all current public employee fields | Projection. |
| `filter` | `status:eq:OFFBOARDED` | Consumer filter example. Mandatory eligibility is still enforced server-side. |
| `sort` | `-terminationDate,employeeId` | Deterministic sorting example. |
| `page` | `1` | 1-based page. |
| `pageSize` | `100` | Page size. |
| `includeTotal` | `false` | Optional count query. |
| `accessToken` | empty | Entra bearer token for APIM. |

## Recommended order

```text
Health / Liveness
Health / Readiness
Metadata / Employee metadata contract
Dynamic Query / Query employees with defaults
Dynamic Query / Query employees with filters and paging
Dynamic Query / Query employees with multiple filters
Negative contract tests
```

See `docs/API_REFERENCE.md` for the full API usage guide.
## Swagger / OpenAPI runtime documentation

When running the Function directly, interactive API documentation is available at:

```text
{{FunctionHost}}/api/swagger
{{FunctionHost}}/api/swagger/index.html
{{FunctionHost}}/api/openapi.yaml
```

For DEV, the default Function App hostname is expected to be:

```text
https://fn-np-d-idbinvest-offboarding.azurewebsites.net/api/swagger
```

The Swagger UI can use **Authorize** with the bearer token and **Try it out** for interactive calls. When testing through APIM, configure the OpenAPI server variable with the DEV APIM hostname.
