# Infrastructure as Code

The supplied **OFFBOARDING INTEGRATION ARCHITECTURE (VIA APIM IN AZURE)** diagram is the physical deployment reference for this repository.

## Diagram synchronization

| Reference design | IaC handling |
| --- | --- |
| `vnet-[env]-intranet-extend` | Existing corporate VNet, supplied through subnet resource IDs. |
| `sn-[env]-apim-idbinvest-stv2` | Existing APIM subnet; not created here. |
| Function integration subnet | Passed as `vnetIntegrationSubnetId`. |
| `sn-pvt-pe-[env]-idbinvest-offboarding` | Passed as `privateEndpointSubnetId`. |
| `rg-[env]-idbinvest` | Existing shared APIM resource group. |
| `rg-[env]-idbinvest-offboarding` | Deployment target resource group for solution-owned resources. The `visuallease` label present in the reference drawing is not used by this implementation. |
| `fn-*-idbinvest-offboarding-facade` | Function naming used by the environment samples. |
| `kv-*-idbi-offboarding` | Key Vault naming used by the environment samples. |
| Storage `sapidbioffboarding` | Logical storage shown in the diagram. The actual account name must be globally unique and environment-specific. It is used only by the Azure Functions runtime/content layer. |
| `ai-*-idbinvest-offboarding` | Application Insights naming used by environment samples. |
| `law-*-idbinvest-offboarding` | Log Analytics naming used by environment samples. |
| Alerts | Azure Monitor alert baseline created by Bicep; approved Action Group IDs are environment inputs. |

APIM is existing corporate infrastructure. This template deploys the Offboarding backend and its solution-owned dependencies, and the APIM policy/OpenAPI assets in this repository configure the **Offboarding Front API** on the existing gateway.
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

Infrastructure does not implement deprovisioning workflows. The deployed Function is read-only. The approved Databricks Gold view is curated and the API also enforces configured eligibility filters and the operational lookback window as defense in depth.

## Azure Functions runtime storage

The Storage Account is host/runtime infrastructure only and is not an offboarding data store. `AzureWebJobsStorage` uses the Function managed identity. Elastic Premium requires an Azure Files content share; its connection string must be created as an approved Key Vault secret and supplied through `azureFilesConnectionSecretUri`. No connection string is committed to source control.

When `enablePrivateEndpoints=true`, provide approved subnet and Private DNS zone IDs for Function App, Blob, File, Queue, Table and Key Vault. Sample parameter files intentionally contain placeholders until corporate network values are supplied.