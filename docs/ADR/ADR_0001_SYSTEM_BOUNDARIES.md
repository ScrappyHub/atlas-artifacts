# ADR 0001: System Boundaries and Isolation

## Status
Accepted

## Context
Atlas Update must remain a standalone product and codebase.

## Decision
We define Atlas Update as independent from any other systems. No shared schemas, registries, assumptions, or deployments. Only reusable architectural patterns are permitted.

## Consequences
- Cleaner security model and threat surface.
- Prevents conceptual drift and governance coupling.

## Alternatives Considered
- “Shared governance substrate”
Rejected due to coupling and boundary violations.
