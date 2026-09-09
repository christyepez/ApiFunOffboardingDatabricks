# Offboarding Final Gap Assessment

## Decision

Implementation status: **Functionally complete / deployment prerequisites pending**.

The solution is a governed, read-only API. It exposes HR-approved offboarding candidates from Databricks and does not execute deprovisioning in downstream applications.

## Closed implementation gaps

| Area | Control / evidence | Status |
| --- | --- | --- |
| Read-only boundary | Only GET HTTP triggers; no command/deprovisioning adapters | Closed |
| Eligibility | `OFFBOARDED` + 30-day server-enforced lookback | Closed |
| Contract isolation | Logical `model` + `contractVersion`; physical Databricks names hidden | Closed |
| Query security | Field/filter/sort whitelist + parameterized SQL | Closed |
| Paging | Deterministic ordering and bounded page size | Closed |
| Databricks results | Multi-chunk retrieval, truncation detection, bounded polling | Closed |
| Error privacy | Provider bodies and submitted PII are not reflected | Closed |
| Correlation | APIM/Function response correlation ID | Closed |
| Health | `/healthcheck`, `/health/live`, `/health/ready`, `/health` alias | Closed |
| API contract | OpenAPI + Postman + metadata contract | Closed |
| APIM security | JWT/app role, rate limit, no-store, Managed Identity backend | Closed |
| Function security | EasyAuth/Entra, `DefaultAzureCredential`, no hardcoded secrets | Closed |
| Observability | App Insights, Log Analytics, diagnostics, 5xx alert baseline | Closed |
| CI contract gate | JSON/OpenAPI/APIM/Bicep validation before central .NET template | Closed |
| Central .NET pipeline | Corporate validation template + pipeline 422 handoff | Closed in YAML |
| Tests | Unit/contract suite plus Postman collection | Closed |
| Architecture sync | `ARCHITECTURE_SYNC.md` maps diagram to implementation | Closed |

## Environment prerequisites before first Azure DevOps deployment

These are not code gaps and must be supplied/authorized by the corporate environment:

1. `SONAR_PROJECT_KEY` pipeline variable and secret `SONAR_TOKEN`.
2. Authorization of Bitbucket service connection `Bitbucket - oscarlo` for `idbinvest/idbisupportnetcodevalidation`.
3. Azure DevOps cross-project permission to invoke deployment pipeline `422`.
4. Function App Registration client ID/audience and APIM Managed Identity client ID.
5. Databricks workspace host, SQL Warehouse ID, approved Gold view and physical column mapping.
6. Databricks grants: `CAN USE`, `USE CATALOG`, `USE SCHEMA`, `SELECT` on the approved view.
7. VNet integration subnet, Private Endpoint subnet and Private DNS zone IDs where private networking is enabled.
8. Key Vault/runtime storage values required by the Function hosting model.
