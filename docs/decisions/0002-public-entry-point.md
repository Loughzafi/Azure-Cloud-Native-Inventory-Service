# ADR 0002: Application Gateway as the intended public entry point

- Status: Accepted as a design direction; not deployed
- Date: 2026-11-25

## Context

The project brief requires a secure public entry point and private database access. The user selected Application Gateway and a development/demo traffic profile with an under-$100/month budget ceiling.

## Decision

Keep Application Gateway as the production-shaped entry point for the initial target architecture. The choice assumes a network-integrated API and private backend path. No region, SKU, WAF policy, or Azure network design is selected yet.

## Alternatives considered

- **Azure Front Door:** not selected because Application Gateway was explicitly chosen for this project; re-evaluate if a global edge or simpler public edge is more appropriate.
- **Direct public Container Apps ingress:** excluded from the target because it weakens the intended single controlled ingress boundary.
- **No public edge:** suitable for local development, but does not demonstrate the project's public-entry-point requirement.

## Consequences and cost gate

Application Gateway, private networking, managed PostgreSQL, registry, secrets, and monitoring could exceed the $100/month ceiling. This project has no measured or quoted Azure cost yet. Do not provision until a region/SKU estimate, budget alert, and teardown path are documented and the user accepts any budget change.

## Revisit when

The cost estimate exceeds the ceiling, the private network topology cannot be implemented as intended, or requirements favor a global edge or lower-cost demo architecture. Any change must be recorded before deployment.
