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
- APIM backend credentials are externalized through named values / Key Vault.
- Databricks runtime identity is read-only and limited to approved views.

## Extensibility

New consuming systems do not require new orchestration logic in this repository. New data contracts are added as governed resources or contract versions without coupling the API to ACBS, Workiva, PeopleSoft, or another consumer.
