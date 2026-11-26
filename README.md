# Azure Cloud-Native Inventory Service

An inventory API portfolio project focused on cloud workload fundamentals: authenticated access, containerization, private data access, observability, and operational evidence. The current deliverable is a tested local application slice; it is **not deployed to Azure**.

## Problem and intended user

Small teams need a simple way to track stock items without exposing their database or credentials. This project provides an authenticated API for creating, listing, retrieving, updating, and deleting inventory records. The business workflow is intentionally small so later iterations can demonstrate secure deployment and operations.

## Role and current scope

This repository is the implementation of the Project 1 portfolio exercise. The checked-in application slice includes the API, PostgreSQL schema, container definition, local development configuration, unit and integration tests, a bounded load-test script, and a local recovery exercise. Azure infrastructure, an executed load test, and production operations have not yet been implemented or measured.

## Architecture

The local implementation runs the ASP.NET Core API and PostgreSQL through Docker Compose. API requests require an Entra ID JWT with the configured delegated scope or app role. The API connects to PostgreSQL over the private Compose network; both published ports are bound to loopback.

The intended Azure shape adds Application Gateway, Azure Container Apps, private PostgreSQL Flexible Server networking, Key Vault accessed by managed identity, and Azure Monitor / Application Insights. Those are design targets, not deployed resources.

See [architecture and trust boundaries](docs/architecture.md) and the [decision records](docs/decisions/).

## Technology and tested scope

| Component | Version / selection | Evidence and limits |
| --- | --- | --- |
| .NET SDK | 10.0.404 | Used for the final build and test run |
| ASP.NET Core | 10.0 | API target and container base-image tag; runtime resolved to 10.0.15 during this run |
| PostgreSQL | 16-alpine | Local Compose database and isolated integration/recovery tests |
| Npgsql | 10.0.4 | Application dependency |
| JWT bearer authentication | 10.0.15 | Application dependency |
| Docker CLI / Compose | 29.9.2 / 5.6.1 | Engine verified; PostgreSQL started healthy |
| Azure services | Not deployed | No Azure subscription resources or live identifiers are included |

These are the versions and scope tested for this iteration, not a claim that the full Azure design has been verified.

## Run and test locally

Prerequisites: .NET 10 SDK and Docker Compose with a running Docker Engine.

1. Publish the API and start the database plus API containers:

   ```powershell
   dotnet publish src\Inventory.Api\Inventory.Api.csproj --configuration Release --output publish /p:UseAppHost=false
   docker compose up --build -d --wait
   ```

   Compose binds PostgreSQL to loopback port 5432 and the API to loopback port 8080. The sample Entra authority and audience are placeholders; override them locally with `AUTHORITY` and `API_AUDIENCE` before starting Compose to test with your app registration.

   Configure the API registration with the `inventory.access` delegated scope and/or the `Inventory.ReadWrite` app role. See the [local development runbook](docs/runbooks/local-development.md).
2. Check `http://localhost:8080/health/live` and `http://localhost:8080/health/ready`.
3. Run the unit tests:

   ```powershell
   dotnet test InventoryService.sln
   ```

For isolated PostgreSQL-backed API integration tests and the bounded local load profile, see the [API testing runbook](docs/runbooks/api-testing.md).

The Compose password is a local-development default only. Never reuse it outside this loopback-bound setup. The schema in `db/schema.sql` runs only when PostgreSQL initializes a new data volume.

## API

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/inventory` | List up to 500 items, ordered by SKU |
| GET | `/api/inventory/{id}` | Retrieve one item |
| POST | `/api/inventory` | Create an item |
| PUT | `/api/inventory/{id}` | Replace an item's SKU, name, and quantity |
| DELETE | `/api/inventory/{id}` | Delete an item |

Requests require a non-empty SKU (up to 64 characters), a non-empty name (up to 200 characters), and a non-negative integer quantity. SKU uniqueness is case-insensitive.

## Security and operations

- Inventory routes require a valid JWT and the configured scope or role. Health endpoints are anonymous; readiness checks PostgreSQL.
- The database port is bound to `127.0.0.1` in local Compose. This is not a substitute for private networking in Azure.
- The API reads its PostgreSQL connection string from configuration. Local development uses an environment variable; the intended Azure design is a Key Vault secret exposed to the Container App through managed identity. That integration is not implemented yet.
- The container uses the non-root user provided by the official ASP.NET runtime image.
- Logs use the JSON console formatter. Azure dashboards, traces, actionable alerts, and diagnostic settings are not configured yet.
- No production backup, restore, rollback, incident-response, or teardown procedure has been exercised. See [current evidence and open checks](docs/evidence/README.md) and the [deployment readiness runbook](docs/runbooks/cloud-deployment.md).

## Decisions, cost, and limitations

Application Gateway is the selected production-shaped entry point. In East US, its Standard v2 fixed charge alone was estimated at **$146/month** for 730 hours, above the requested **under-$100/month** ceiling and excluding other costs. See the [preliminary cost gate](docs/evidence/east-us-cost.md). Do not provision until all selected SKUs are priced and the budget trade-off is accepted.

The service currently has no Bicep, Azure deployment, database migration framework, pagination beyond a fixed 500-item limit, executed load test, Azure recovery exercise, or Azure telemetry configuration. See the [decision records](docs/decisions/) for alternatives and reconsideration conditions.

## Verified results

As of 2026-11-25, the .NET 10 test run passed **4 of 4 unit tests**, the Release build and publish succeeded, and the PostgreSQL 16 container started healthy. The `inventory-api:local` image built successfully and ran as UID 1654. An API smoke check against the Compose stack observed liveness `200`, readiness `200`, and `401` on an unauthenticated inventory request. A database check verified case-insensitive SKU uniqueness and left no smoke-test row behind. The Compose file passed configuration validation. The first multi-stage image build was stopped during a slow SDK image download; the documented workflow publishes on the host and packages the output in the ASP.NET runtime image.

On 2026-11-26, a synthetic local PostgreSQL backup/restore exercise took 1.52 seconds from source stop through verification on an already-running target; one backed-up record was recovered and one record written after backup was absent. This is not an Azure RTO or a time-based RPO. See the [recovery runbook](docs/runbooks/local-recovery.md) and [evidence](docs/evidence/README.md).

On the same date, **2 of 2 integration tests** passed against an isolated PostgreSQL container and a test-only JWT issuer. The bounded load-test script has not been run.

The [Azure recovery runbook](docs/runbooks/azure-recovery.md) is a pre-deployment plan, not an exercised procedure. An Azure recovery test creates billable resources and requires separate approval.

These are local results only. No Azure deployment, load test, actual Azure cost measurement, or Azure backup/restore exercise has been completed; there are no measured cloud performance or recovery claims.

## Next iterations

1. Implement and validate the Bicep workload and network modules, including private database access, managed identity, Key Vault, and diagnostics.
2. Price the selected Azure region and SKUs against the monthly ceiling before any deployment.
3. Run the bounded load test against an isolated local API with a valid test token; record workload, date, and environment size.
4. After pricing and separate deployment approval, exercise Azure backup and restore and record observed recovery and data loss.

Do not present planned work or example résumé bullets as completed results.
