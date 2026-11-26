# ADR 0003: Entra ID for callers and managed identity for Azure services

- Status: Accepted as an application/deployment design; Azure identity wiring is pending
- Date: 2026-11-25

## Context

The API is intended to be publicly reachable through a controlled edge while the database and runtime secrets remain private. The user selected Microsoft Entra ID JWT bearer authentication.

## Decision

Validate Entra-issued bearer tokens using a configured HTTPS authority and audience. Permit inventory operations only when the token has the configured `inventory.access` scope or `Inventory.ReadWrite` role. For the future Azure deployment, use a managed identity for the API's Key Vault secret read and registry image pull; map the database connection secret to `ConnectionStrings__Postgres`.

## Alternatives considered

- **Shared API key:** not selected because per-user/app token validation and roles/scopes provide clearer identity semantics.
- **Anonymous inventory API:** rejected because it exposes business data and mutation operations.
- **Committed connection string or deployment parameter secret:** rejected because secrets must not be stored in source or ordinary deployment parameters.
- **Service principal credentials:** not selected for Azure runtime access; managed identity avoids an application-managed credential.

## Consequences and limitations

- Local and Azure app-registration configuration is required to exercise authorized calls; no tenant or application identifiers are committed.
- Health endpoints are anonymous and disclose only probe status.
- Managed identity, Key Vault role assignments, registry pull permissions, and secret mapping are not implemented until Azure infrastructure exists.
- Token claim expectations and authorization behavior should receive integration tests with an isolated test issuer before production use.

## Revisit when

The API gains finer-grained permissions, machine-to-machine workloads need a distinct app role, or the selected hosting platform changes its identity integration.
