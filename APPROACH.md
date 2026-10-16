# Technical Approach & Thought Process

This document outlines the architectural approach, key technical trade-offs, and reasoning across the tasks implemented in the Northwind Support Desk project.

---

## 1. Architectural Philosophy

The application follows **Clean Architecture** and **Domain-Driven Design (DDD)** principles:
- **`SupportDesk.Domain`**: Pure business domain. Contains aggregates (`Ticket`, `Agent`, `Customer`, `Category`, `TicketEscalation`), value objects, and domain repository interfaces. No third-party framework dependencies.
- **`SupportDesk.Application`**: Use case orchestration (CQRS Commands and Queries), FluentValidation validators, DTOs, and abstractions.
- **`SupportDesk.Infrastructure`**: EF Core persistence, SQL Server mapping, migrations, database seeding, and security primitives (PBKDF2 password hasher, JWT token service).
- **`SupportDesk.Presentation`**: ASP.NET Core Web API controllers, custom middleware, central authorization policies, and health check endpoints.
- **`apps/web`**: React 19 + TypeScript frontend with modular feature organization, typed API clients, and accessible UI controls.

---

## 2. Task-by-Task Implementation Approach

### Task 1: Defect Fix & Server-Side Filtering
- **Defect Fix (`DEFECT-117`)**:
  - *Observation*: Changing dropdown filters sent no HTTP requests until typing in search.
  - *Root Cause*: Filter state was being debounced or throttled improperly together with the text input, preventing immediate state propagation to the query hook.
  - *Resolution*: Separated immediate filter triggers from debounced text search, with a regression test ensuring each dropdown change immediately fires a network request.
- **Server-Side Filtering**:
  - Translated all filter parameters (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `search`, and Task 2's `slaStatus`) directly into EF Core `IQueryable<Ticket>` expressions.
  - Applied `Where` predicates in SQL *before* computing `CountAsync()` and `Skip/Take`, avoiding in-memory C# evaluation and guaranteeing accurate pagination metadata (`totalCount`, `totalPages`).

### Task 2: Ticket Escalation & Assignment
- **Automated Triage Engine**:
  - Implemented configurable business rules (`BR-1` through `BR-6`):
    - Priority selection honoring `ForcesCriticalPriority` categories.
    - SLA window calculation honoring Premium customer multipliers (half window with a 1-hour floor).
    - Workload-aware agent assignment selecting the active, qualified specialist with the lowest open ticket count, breaking ties deterministically by ID.
    - Graceful fallback: when no agent is eligible, the ticket is created unassigned with an explicit human-readable reason.
- **Escalation & History**:
  - Enforced single-level priority advancement (`Low` -> `Medium` -> `High` -> `Critical`) while rejecting terminal states (`Critical`, `Resolved`, `Closed`) with HTTP 409 Conflict.
  - Due dates recomputed from the timestamp of escalation.
  - Recorded immutable history rows in `TicketEscalations` with before/after snapshots for auditability.
- **Derived SLA Status**:
  - SLA status is derived dynamically (`NotApplicable`, `WithinSla`, `AtRisk`, `Breached`, `Met`), never stored in the database.
  - Injected `ISystemClock` abstraction across all calculations to allow deterministic unit testing without wall-clock flakiness.
- **Task 2.5 Optional Stretch — SLA Summary Report**:
  - *Single Database-Side Aggregation*: Implemented `GET /api/reports/sla-summary?from=&to=&groupBy=agent|priority` executing entirely inside the database engine via a single EF Core `GroupBy` query.
  - *No In-Memory Grouping*: Avoided loading tickets into C# memory. SQL Server executes `COUNT(*)`, conditional aggregations (`SUM(CASE WHEN ... THEN 1 ELSE 0 END)`), and `AVG(DATEDIFF(minute, CreatedAtUtc, ResolvedAtUtc) / 60.0)` directly.
  - *Unassigned Ticket Preservation*: Left-joined `Tickets` against `Agents` so unassigned tickets are accurately represented as `GroupKey = "unassigned"`, `GroupLabel = "Unassigned"` rather than being omitted.
  - *Clean Architecture & SOLID*: Decoupled query orchestration into `GetSlaSummaryReportQueryHandler`, abstract seam `ISlaReportQueries`, and `SlaReportQueries` in Infrastructure.
  - *End-to-End Frontend View*: Built an accessible reports dashboard in React 19 (`/reports`) with real-time grouping toggles, date filters, summary cards, and SLA breakdown tables.

### Task 3: Login and Authentication
- **Credential Storage**:
  - Stored `PasswordHash` directly on the `Agent` aggregate via a committed EF Core migration, reflecting that every authenticated user is an internal support agent.
- **Password Security**:
  - Applied industry-standard PBKDF2 with HMAC-SHA256, 16-byte cryptographically secure random salt, and 100,000 iterations.
  - Used `CryptographicOperations.FixedTimeEquals` to thwart timing attacks.
  - Seeded local development credentials (`LocalDev123!`) hashed at seed time.
- **Central Authorization**:
  - Adopted ASP.NET Core `FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`.
  - Enforced authentication globally without relying on error-prone per-controller `[Authorize]` decorations.
  - Kept only `POST /api/auth/login`, Swagger, and `GET /health` publicly accessible via explicit `AllowAnonymous()`.
- **Identity Integrity**:
  - Extracted the actor (`escalatedBy`) directly from JWT token claims (`User.Identity.Name`), eliminating client-side actor tampering.

### Task 4: Dockerising the API
- **Layer Caching Architecture (`DK-1`)**:
  - Adopted a multi-stage Docker build separating SDK compilation from ASP.NET runtime execution.
  - Extracted project files, `global.json`, `Directory.Build.props`, and `Directory.Packages.props` into an early restore stage.
  - Because `dotnet restore` is executed before source code is copied, Docker caches the restored NuGet packages layer across all source code edits, cutting rebuild times down to seconds.
- **Container Hardening (`DK-3`)**:
  - Configured `USER $APP_UID` (evaluating to UID 1654, user `app`), ensuring the process runs with non-root privileges.
  - Bound to unprivileged HTTP port 8080 (`ASPNETCORE_HTTP_PORTS=8080`).
  - No certificates or development tooling are bundled into the production image.
- **Configuration & Secret Isolation (`DK-4`)**:
  - Completely detached runtime secrets from the container image.
  - Connection strings, signing keys, and environment settings are injected strictly via container environment variables in `docker-compose.yml`.
- **Health Check Strategy (`DK-7`)**:
  - Built a `DatabaseHealthCheck` inside `SupportDesk.Presentation` implementing `IHealthCheck` to test `SupportDbContext.Database.CanConnectAsync()`.
  - Exposed via `app.MapHealthChecks("/health").AllowAnonymous();`, providing external orchestrators with an unauthenticated liveness and readiness probe.
- **Production Migration Rationale (`DK-6`)**:
  - In local development, `await app.MigrateAndSeedAsync()` bootstraps a working database automatically on an empty volume.
  - In production, migrations should run outside the API process (via CI/CD migration scripts `dotnet ef migrations script --idempotent` or a dedicated deployment `initContainer` / Migration Bundle) to prevent concurrent migration collisions between autoscaled replicas and adhere to the principle of least privilege (API runtime service account should have DML only, not DDL).
