# Evidence and acceptance status

This page separates completed local checks from Azure work that has not been run. No Azure resources were provisioned.

## Measured / verified

| Check | Result | Scope and limitation |
| --- | --- | --- |
| `dotnet test InventoryService.sln` (2026-11-25) | 4 passed, 0 failed | Unit tests for inventory request validation; no database or HTTP integration tests |
| `dotnet build InventoryService.sln --configuration Release --no-restore` | Succeeded, 0 warnings, 0 errors | .NET 10 Release build |
| Release publish | Succeeded | .NET 10 application publish |
| `docker compose up -d --wait postgres` | PostgreSQL 16-alpine reported healthy | Local Docker Desktop; Compose volume persists until explicitly removed |
| `docker compose build api` | `inventory-api:local` built successfully | Host-published .NET 10 output packaged in the ASP.NET runtime image |
| Compose API container | Running as UID 1654; liveness 200; readiness 200; unauthenticated inventory request 401 | Local API container connected to PostgreSQL; no valid-token CRUD request exercised |
| PostgreSQL schema check | Case-insensitive SKU duplicate rejected; test row removed | Executed in a DO block against local PostgreSQL; not an API/database integration test |
| `docker compose config --quiet` | Succeeded | Compose syntax/configuration validation |
| Local PostgreSQL restore (2026-11-26 15:23 UTC) | 1.52 seconds from source stop to verified restore; 1 backed-up row recovered, 1 later row absent | Two isolated PostgreSQL 16-alpine containers; synthetic data; target was already running; not Azure RTO or time-based RPO |
| Local PostgreSQL restore repeat (2026-11-26 15:26 UTC) | 1.94 seconds; 1 backed-up row recovered, 1 later row absent | Same profile; exercise containers were removed after the run |
| API integration tests (2026-11-26) | 2 passed, 0 failed | In-process API, isolated PostgreSQL 16-alpine container, and test-only JWT issuer; validates CRUD and authorization but not real Entra ID or Azure networking |
| Bounded load script (2026-11-26) | Syntax checked; not executed | k6 and a valid token were unavailable; no latency or throughput result |

## Acceptance checklist

- [x] Inventory API project, container definition, local database schema, and Compose configuration exist.
- [x] Inventory routes require authentication and authorization; anonymous liveness/readiness probes are configured.
- [x] Local unit tests, PostgreSQL startup/schema, and API health/auth smoke behavior have been verified.
- [x] Local API image built and started using the host-publish workflow; health and auth smoke checks passed.
- [x] Repeatable isolated PostgreSQL-backed API integration tests, including JWT authorization and inventory CRUD.
- [ ] Clean Azure deployment from documented Bicep commands.
- [ ] Private-only Azure PostgreSQL connectivity and verified network policy.
- [ ] Key Vault secret access through a deployed managed identity.
- [ ] Azure health probes, autoscaling, resource limits, diagnostics, dashboards, and actionable alerts.
- [ ] Regional cost estimate, budget alert, and reviewed safe teardown.
- [ ] Repeatable load profile and measured performance result.
- [x] Local backup/restore exercise with observed duration and synthetic data-loss outcome.
- [ ] Azure backup/restore exercise with observed recovery time and data-loss outcome.
- [ ] Sanitized Azure deployment/evidence export.

## Not measured or proven

- Any Azure deployment, monthly cost, database network exposure, or managed identity behavior
- Authorized API request against a real test Entra issuer
- Load, latency, availability, autoscaling, or capacity
- Azure backup, restore, RTO, RPO, rollback, or incident recovery

There are no screenshots or Azure logs yet. Add evidence only after performing the checks, and redact tokens, connection strings, tenant/resource identifiers, customer data, and other sensitive values.
