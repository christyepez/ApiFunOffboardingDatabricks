# Offboarding Data Exposure - TO-BE

## Scope

The solution exposes governed offboarding data from Databricks to authorized consumer systems. It is intentionally read-only and does not execute deprovisioning in downstream applications.

## Target flow

PeopleSoft / HR -> DataMotion / Databricks -> Azure Function -> APIM -> Consumer Systems.

Each consumer system owns its own offboarding workflow, writes, retries, application-specific validation, and evidence.

## Configuration-driven contract

`resource-definitions.json` defines the public exposure model. Each resource contains a logical `model`, `contractVersion`, approved physical source, public field aliases, selectable/filterable/sortable capabilities, default fields, and maximum page size.

## Security boundaries

- Consumers never submit raw SQL or physical Databricks identifiers.
- Query values are parameterized.
- APIM validates Microsoft Entra ID roles and applies throttling/correlation.
- APIM authenticates to the Function backend using its Managed Identity; Function App EasyAuth permits only the approved APIM application identity.
- Databricks runtime identity is read-only and limited to approved views.

## Extensibility

New consuming systems do not require new orchestration logic in this repository. New data contracts are added as governed resources or contract versions without coupling the API to ACBS, Workiva, PeopleSoft, or another consumer.

## Source population contract

The approved Databricks Gold view is the curated source, and the API independently enforces the HR-approved eligibility boundary as defense in depth: `status = OFFBOARDED` plus the configured 30-day termination-date lookback. The API does not identify or deactivate users itself.

The logical contract exposes `employeeId` as the primary correlation identifier and `email` as supporting identity data. Consumers must not assume email is globally unique or that application-specific usernames match corporate email values.

## Enforced exposure boundary

The API independently enforces the offboarding eligibility boundary configured in the logical resource definition. For the initial employee contract, `status = OFFBOARDED` and a 30-day termination-date lookback are mandatory server filters. Consumer filters are additive only. A deterministic server default sort protects page ordering.

The Databricks adapter follows all INLINE result chunks and fails closed if Databricks reports a truncated result or the configured chunk safety limit is exceeded. This prevents silent partial datasets from being presented as complete responses.

Azure Storage resources in the deployment template support the Azure Functions runtime only; this solution does not persist business-process state.