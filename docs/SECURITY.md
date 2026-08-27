# Security Model

## Security objectives

The API facade is designed to keep Databricks implementation details private, enforce least privilege, and expose only approved data contracts through API Management.

## Trust boundaries

```text
Consumer
  -> Microsoft Entra ID / OAuth 2.0
  -> Azure API Management
  -> Azure Function App
  -> Databricks Statement Execution API
  -> SQL Warehouse
  -> Unity Catalog
  -> Approved Gold views
```

## Consumer to APIM

Recommended controls:

- Microsoft Entra ID OAuth 2.0 access tokens;
- `validate-azure-ad-token` or approved JWT validation policy;
- application roles/scopes per consumer type;
- rate limits and quotas;
- optional subscription keys as a secondary governance control, not a replacement for identity;
- mTLS or IP filtering where required for partner integrations.

## APIM to Function

Preferred mechanism: Managed Identity.

The Function should not be freely reachable from the Internet. Use private networking where supported by the selected tiers, or strict access restrictions so APIM is the only intended ingress path.

## Function to Databricks

Preferred authentication order:

1. Managed/Federated identity when supported by the target workspace design;
2. Service Principal OAuth;
3. PAT only for local development or exceptional temporary use.

Required Databricks privileges should be limited to:

- `CAN USE` on the designated SQL Warehouse;
- `USE CATALOG` on approved catalogs;
- `USE SCHEMA` on approved schemas;
- `SELECT` on approved API-facing views.

Do not grant `OWNERSHIP`, `MODIFY`, `CREATE`, or broad workspace administrator privileges to the runtime identity.

## SQL injection prevention

The API never accepts raw SQL. Query construction is controlled by:

- resource whitelist;
- public-field-to-physical-column mapping;
- explicit selectable/filterable/sortable flags;
- explicit operator whitelist;
- parameterized Databricks query values;
- page-size limits.

Caller-provided values must never be concatenated into SQL strings.

## Secret management

- No secrets in source control.
- Use Azure Key Vault for any secret that cannot be removed through identity-based authentication.
- Enable soft delete and purge protection.
- Restrict Key Vault networking where possible.
- Rotate secrets according to enterprise policy.
- Never log tokens, passwords, PATs, Function keys, or authorization headers.

## Data exposure rules

- Expose only fields required by integration contracts.
- Prefer dedicated Gold views shaped for API consumption.
- Do not expose raw Databricks error payloads, manifests, statement IDs, catalog names, schema names, view names, or physical columns.
- Apply data classification before exposing personally identifiable or confidential data.

## Logging and privacy

Logs should contain correlation IDs, resource names, timing, status codes, and operational dimensions. Avoid request/response payload logging when payloads may contain confidential information.

## Security testing checklist

- unauthorized request -> 401;
- authenticated but unauthorized request -> 403;
- unknown fields/operators -> 400;
- SQL-like values remain parameters and are not executable SQL;
- physical identifiers are absent from public responses;
- secrets are absent from repository and pipeline logs;
- Function cannot be bypassed around APIM where network policy requires APIM-only access;
- Databricks identity cannot write or alter source data;
- excessive page size is rejected;
- dependency errors return sanitized 503/500 contracts.

## Production hardening

Before production, confirm APIM and Function network topology, Private Endpoints/Private DNS where required, diagnostic settings, Entra application roles, Key Vault RBAC, Databricks grants, log retention, Defender recommendations, cost budgets, and incident alerting.
