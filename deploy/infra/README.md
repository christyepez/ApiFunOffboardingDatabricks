# Infrastructure as Code

`main.bicep` is the deployment baseline for the Offboarding Data Exposure API. It is intentionally parameterized so DEV, TEST and PROD can reuse the same template without embedding environment credentials or corporate network values.

## Resources

- Linux Azure Functions on Elastic Premium (`EP1`)
- System-assigned managed identity
- Storage account for the Azure Functions host, using identity-based `AzureWebJobsStorage`
- Log Analytics workspace
- Workspace-based Application Insights
- Azure Key Vault with RBAC and soft delete
- Function App EasyAuth / Microsoft Entra ID

APIM is assumed to be the existing corporate API Management service and is not created by this template. `deploy/apim/inbound-policy.xml` is imported into that existing APIM API.

## Authentication path

Consumer -> APIM: Microsoft Entra JWT with `Offboarding.Read`.

APIM -> Function App: APIM Managed Identity obtains a token for `functionAppAudience`. EasyAuth on the Function App requires authentication and restricts the allowed client application to `apimManagedIdentityClientId`.

Function App -> Databricks: system-assigned managed identity / `DefaultAzureCredential`.

No Function host key is required on this path.
## Environment samples

Sample parameter files are provided under `parameters/` for DEV, TEST and PROD. Values containing `REPLACE` are deployment inputs and must be supplied by the environment pipeline or approved variable group.

Required environment inputs include:

- Function App registration client ID and API audience
- APIM managed identity client ID
- Databricks workspace URL and SQL Warehouse ID
- globally unique Storage Account name
- approved resource naming/tags

## Network boundary

The template exposes switches for Function, Storage and Key Vault public network access. The sample files leave them enabled because the corporate VNet/subnet/private-DNS resource IDs and CIDRs are environment-owned inputs and have not been committed to source control.

For a private-only deployment, supply the approved network design and add the corresponding private endpoints/VNet integration before setting these switches to `false`. Do not invent CIDRs inside this repository.

## Data scope

Infrastructure does not implement deprovisioning workflows. The deployed Function is read-only and exposes only approved Databricks Gold views. The approved offboarding view must enforce the HR-defined offboarding population (including the operational lookback window) before data is exposed.
