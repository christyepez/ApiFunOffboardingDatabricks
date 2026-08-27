# Configuration Reference

## General rules

Configuration must be environment-specific and injected through Azure Function App settings, deployment templates, or approved configuration stores. Secrets must not be committed to Git.

## Function configuration

Typical settings:

| Setting | Purpose | Secret |
|---|---|---:|
| `Databricks__Host` | Databricks workspace base URL. | No |
| `Databricks__WarehouseId` | SQL Warehouse identifier. | No |
| `Databricks__AuthenticationMode` | `DefaultAzureCredential` or local-only `PAT`. | No |
| `Databricks__Token` | Local PAT only when explicitly required. | Yes |
| `Databricks__WaitTimeoutSeconds` | Statement wait timeout. | No |
| `Databricks__PollIntervalMilliseconds` | Poll interval for async statements. | No |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights connection. | Treat as configuration |
| `AzureWebJobsStorage` | Azure Functions runtime storage. | Yes/connection secret unless identity-based configuration is used |

Use the exact names implemented by the current Functions configuration binding. If naming changes, update this file and deployment templates together.

## Resource registry

`resource-definitions.json` is a governance control, not merely convenience configuration.

Example:

```json
{
  "name": "employees",
  "source": "gold.offboarding.vw_employees",
  "maxPageSize": 1000,
  "defaultFields": ["employeeId", "fullName", "status"],
  "fields": {
    "employeeId": {
      "column": "employee_id",
      "type": "STRING",
      "selectable": true,
      "filterable": true,
      "sortable": true
    }
  }
}
```

Rules:

- `name` is the stable public resource identifier.
- `source` is internal and must never be accepted from an HTTP caller.
- public field keys are API contract names.
- `column` is internal Databricks metadata.
- set capabilities explicitly.
- use the smallest practical `maxPageSize`.
- expose dedicated views rather than broad source tables where possible.

## Environment matrix

Maintain at least DEV, QA, and PROD with separate runtime configuration and identities.

| Area | DEV | QA | PROD |
|---|---|---|---|
| Function App | dedicated | dedicated | dedicated |
| APIM API/revision | DEV | QA | PROD |
| Databricks warehouse | non-prod | non-prod/QA | prod |
| Runtime identity | dedicated | dedicated | dedicated |
| Key Vault | dedicated/shared by policy | dedicated/shared by policy | dedicated/restricted |
| Logs | enabled | enabled | enabled with production retention |

## Authentication modes

### Production

Use `DefaultAzureCredential`/Managed Identity, workload identity, or Service Principal OAuth according to the Azure/Databricks platform design.

### Local development

A developer PAT may be used only when necessary. Store it in `local.settings.json` or the developer's secret store and never commit it.

## APIM named values

Use APIM named values/Key Vault references for environment-specific values such as backend URL, Entra tenant/application IDs, audiences, or policy parameters. Do not hard-code secrets in policy XML.

## Networking

Expected production controls may include:

- VNet integration for Function App;
- Private Endpoints for Key Vault/Storage where applicable;
- Private DNS zones;
- APIM VNet integration depending on SKU/topology;
- controlled egress to Databricks over HTTPS or Private Link design;
- Function access restrictions preventing direct consumer bypass.

## Configuration change process

Any change to resource mappings, maximum page sizes, authentication, permissions, or APIM policy behavior must be peer-reviewed and promoted through environments. Configuration that changes the public API contract must also update OpenAPI, Postman tests, and documentation.
