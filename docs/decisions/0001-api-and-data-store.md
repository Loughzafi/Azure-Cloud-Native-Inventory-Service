# ADR 0001: ASP.NET Core API and PostgreSQL data store

- Status: Accepted for the local vertical slice
- Date: 2026-11-25

## Context

Project 1 needs a small business workflow that leaves room to demonstrate container hosting, identity, network boundaries, monitoring, and recovery. The user selected an inventory service and C# with ASP.NET Core.

## Decision

Implement a REST API on ASP.NET Core 10 LTS and store inventory records in PostgreSQL. Use parameterized Npgsql commands, a database-enforced case-insensitive unique SKU index, and a bounded list endpoint (500 records).

## Alternatives considered

- **Order workflow:** rejected for this iteration because inventory is the selected, smaller core scenario.
- **TypeScript or Python API:** not selected because C# / ASP.NET Core was selected.
- **Document database:** not selected; inventory records and SKU uniqueness fit the relational constraints and PostgreSQL in the project brief.
- **In-memory persistence:** rejected because it would not exercise the intended database boundary.

## Consequences

- The API and relational constraints can be tested independently of Azure.
- PostgreSQL schema setup is currently a one-time initialization script, not a versioned migration system.
- The API list endpoint is intentionally bounded but has no pagination contract yet.
- Azure database deployment and its private connectivity remain unimplemented.

## Revisit when

The workflow grows beyond the demo, pagination or migration requirements become real, or measured workload and operational needs justify changing the persistence model.
