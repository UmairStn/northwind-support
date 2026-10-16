# Technical Approach & Thought Process

This document outlines the architectural approach, key technical trade-offs, design patterns, and engineering reasoning across the tasks implemented in the Northwind Support Desk project.

---

## 1. Architectural Philosophy & Clean Architecture

The system is structured according to **Clean Architecture** and **Domain-Driven Design (DDD)** principles to guarantee separation of concerns, testability, and framework independence:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation (API / UI)                  │
│       Controllers, Middlewares, React 19 Client Features     │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                    Application (Use Cases)                   │
│        CQRS Commands & Queries, DTOs, FluentValidation      │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                     Domain (Core Logic)                     │
│    Aggregates, Domain Policies (SLA, Priority, Assignment)  │
└──────────────────────────────▲──────────────────────────────┘
                               │
┌──────────────────────────────┴──────────────────────────────┐
│                   Infrastructure (Adapters)                 │
│      EF Core Mapping, SQL Server, PBKDF2 Hasher, JWT Auth   │
└─────────────────────────────────────────────────────────────┘
```

- **`SupportDesk.Domain`**: Pure business domain. Contains aggregates (`Ticket`, `Agent`, `Customer`, `Category`, `TicketEscalation`), value objects, and domain policies (`SlaPolicy`, `PriorityPolicy`, `AgentAssignmentService`). Zero third-party dependencies or framework coupling.
- **`SupportDesk.Application`**: Use case orchestration (CQRS Commands and Queries), FluentValidation validators, DTOs, and abstractions (`ISystemClock`, `ITicketQueries`).
- **`SupportDesk.Infrastructure`**: EF Core persistence, SQL Server mapping, migrations, database seeding, and security primitives (PBKDF2 password hasher, JWT token service).
- **`SupportDesk.Presentation`**: ASP.NET Core Web API controllers, custom middleware, central authorization policies, and health check endpoints.
- **`apps/web`**: React 19 + TypeScript frontend with modular feature organization, typed API clients, and accessible UI controls.

---

## 2. Design Patterns Applied & Justified

The assessment emphasizes: *"Two or three design patterns that genuinely fit, each justified."* Rather than adopting heavyweight abstractions for their own sake, we applied three high-value patterns:

### 1. Strategy / Policy Pattern (`SlaPolicy`, `PriorityPolicy`)
- **Where Applied**: `SupportDesk.Domain/Policies/SlaPolicy.cs` and `PriorityPolicy.cs`.
- **Why It Fits**: Both ticket creation (`CreateTicketCommandHandler`) and ticket escalation (`EscalateTicketCommandHandler`) must compute SLA target windows, check customer tiers, enforce minimum floors, and determine priority rules. Rather than duplicating logic or placing it inside entity methods with hidden dependencies, the rules are encapsulated in stateless, pure policies.
- **Benefits**: Completely testable with simple unit tests; easily configurable via `IOptions<SlaOptions>`; zero side effects.

### 2. Query Object Pattern (`TicketFilters.cs`)
- **Where Applied**: `SupportDesk.Infrastructure/Queries/Filters/TicketFilters.cs`.
- **Why It Fits**: The list query must combine 8 different filter criteria (status, priority, category, customer, agent, unassigned-only, search text, and dynamic SLA status) in arbitrary combinations using logical `AND`.
- **Benefits**: Translates complex domain filter criteria into composable `IQueryable<Ticket>` expressions. Pushes all predicate evaluation directly into SQL Server before `CountAsync()` and `Skip/Take`, avoiding in-memory C# evaluation and guaranteeing exact pagination.

### 3. Factory / Domain Service Pattern (`AgentAssignmentService`)
- **Where Applied**: `SupportDesk.Domain/Policies/AgentAssignmentService.cs`.
- **Why It Fits**: Triaging a ticket requires evaluating multiple agent constraints (active status, ticket limit ceilings, specialist category tags, and current open ticket counts) with deterministic tie-breaking.
- **Benefits**: Isolates assignment business logic from both the `Ticket` aggregate and application command handlers, providing a single reusable service for initial triage and escalation re-evaluation.

---

## 3. Task-by-Task Implementation Approach

### Task 1: Defect Fix & Server-Side Filtering
- **Defect Fix (`DEFECT-117`)**:
  - *Observation*: Changing dropdown filters sent no HTTP requests until typing in search.
  - *Root Cause*: In `useTicketList.ts`, filter state and text search state were entangled in a single debounced flow, preventing immediate parameter propagation to the query hook.
  - *Resolution*: Decoupled immediate dropdown filter state changes from debounced search term updates. Any dropdown change immediately dispatches `GET /api/tickets`, while typing in search remains debounced (300ms) to avoid request storms.
  - *Regression Test*: Added tests in `useTicketList.test.ts` asserting an immediate request is triggered on filter changes.
- **Server-Side Filtering (`FL-1` to `FL-6`)**:
  - Implemented `TicketFilters.ApplyFilters` extension method on `IQueryable<Ticket>`.
  - All filters (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`) map to exact SQL equality.
  - `unassignedOnly=true` filters for `t.AssignedAgentId == null`.
  - `search` is trimmed and searches ticket reference, title, or customer name via SQL `LIKE`/`Contains`.
  - Evaluated in SQL *before* computing `CountAsync()` and `Skip/Take`, ensuring 100% accurate pager counts (`totalCount`, `totalPages`).

### Task 2: Ticket Escalation & Assignment
- **Automated Triage Engine (`BR-1` to `BR-6`)**:
  - Categories flagged `ForcesCriticalPriority` (Security, Outage) always force `Critical`.
  - SLA windows calculated based on priority (Critical 4h, High 8h, Medium 24h, Low 72h), halved for Premium customers with a strict 1-hour floor.
  - Workload-aware assignment selects active specialists under their `MaxOpenTickets` with the fewest open tickets, breaking ties deterministically by agent `Id`.
  - Normal fallback: If no agent is eligible, the ticket is created unassigned with an explicit human-readable reason (HTTP 201).
- **Escalation & Audit History (`BR-7`)**:
  - Advances priority by one step (`Low` → `Medium` → `High` → `Critical`), recomputes due date from the escalation timestamp, re-evaluates agent eligibility, and records an immutable history row in `TicketEscalations`.
  - Terminal states (`Critical`, `Resolved`, `Closed`) are rejected with HTTP 409 Conflict.
- **Derived SLA Status (`BR-8`)**:
  - SLA status is derived dynamically (`WithinSla`, `AtRisk`, `Breached`, `Met`, `NotApplicable`) and never stored in the database.
  - Injected `ISystemClock` abstraction across all calculations to allow deterministic unit testing without wall-clock flakiness.
- **Database Indexing**:
  - Added composite descending index `IX_TicketEscalations_TicketId_EscalatedAtUtc` (`TicketId ASC, EscalatedAtUtc DESC`). Escalation history is queried per ticket ordered newest first; the index allows SQL Server to seek directly without sorting in memory.

### Task 2.5: Optional Stretch — SLA Summary Report
- **Single Database-Side Aggregation**:
  - Implemented `GET /api/reports/sla-summary?from=&to=&groupBy=agent|priority` executing entirely inside SQL Server via a single EF Core `GroupBy` query.
  - Aggregates total tickets (`COUNT(*)`), dynamic SLA status counts (`SUM(CASE WHEN ... THEN 1 ELSE 0 END)`), breach rates, and average resolution hours (`AVG(DATEDIFF(minute, CreatedAtUtc, ResolvedAtUtc) / 60.0)`).
  - Left outer joins `Tickets` against `Agents` so unassigned tickets are preserved under `GroupKey = "unassigned"`, `GroupLabel = "Unassigned"`.
  - Built an accessible reports dashboard in React 19 (`/reports`) with real-time grouping toggles, date filters, summary cards, and SLA breakdown tables.

### Task 3: Login and Authentication
- **Credential Storage on `Agent` Aggregate (`AU-1`)**:
  - Stored `PasswordHash` directly on `Agent` via committed migration `AddAgentPasswordHash`. In Northwind Support Desk, every authenticated user is an internal support agent; storing credentials on `Agent` avoids unnecessary table joins and multi-aggregate synchronization.
- **Password Security (`AU-2`, `AU-3`)**:
  - Applied industry-standard PBKDF2 with HMAC-SHA256, 16-byte cryptographically secure random salt, and 100,000 iterations.
  - Used `CryptographicOperations.FixedTimeEquals` to thwart timing side-channel attacks.
  - Seeded local development credentials (`LocalDev123!`) hashed at seed time.
- **Central Authorization (`AU-6`)**:
  - Adopted ASP.NET Core `FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`.
  - Enforced authentication globally without relying on error-prone per-controller `[Authorize]` decorations.
  - Kept only `POST /api/auth/login`, Swagger, and `GET /health` publicly accessible via explicit `AllowAnonymous()`.
- **Identity Integrity (`AU-8`)**:
  - Extracted actor (`escalatedBy`) directly from JWT token claims (`User.Identity.Name`), eliminating client-side actor tampering.

### Task 4: Dockerising the API
- **Layer Caching Architecture (`DK-1`)**:
  - Multi-stage Docker build separating SDK compilation (`mcr.microsoft.com/dotnet/sdk:10.0`) from ASP.NET runtime execution (`mcr.microsoft.com/dotnet/aspnet:10.0`).
  - Extracted project files, `global.json`, `Directory.Build.props`, and `Directory.Packages.props` into an early restore stage. Subsequent code edits reuse the cached NuGet package layer (`CACHED`), cutting rebuild times to seconds.
- **Container Hardening (`DK-3`)**:
  - Configured `USER $APP_UID` (UID 1654, user `app`), ensuring the process runs with non-root privileges.
  - Bound to unprivileged HTTP port 8080 (`ASPNETCORE_HTTP_PORTS=8080`).
- **Configuration & Secret Isolation (`DK-4`)**:
  - Connection strings, signing keys, and environment settings are injected strictly via container environment variables in `docker-compose.yml`.
- **Health Check Strategy (`DK-7`)**:
  - Built `DatabaseHealthCheck` implementing `IHealthCheck` to test `SupportDbContext.Database.CanConnectAsync()`.
  - Exposed via `app.MapHealthChecks("/health").AllowAnonymous();`, providing external orchestrators with an unauthenticated liveness and readiness probe.
- **Production Migration Rationale (`DK-6`)**:
  - In local development, `await app.MigrateAndSeedAsync()` bootstraps a working database automatically on an empty volume.
  - In production, migrations should run outside the API process (via CI/CD migration scripts `dotnet ef migrations script --idempotent` or a dedicated deployment `initContainer` / Migration Bundle) to prevent concurrent migration collisions between autoscaled replicas and adhere to the principle of least privilege.

---

## 4. Scaling Considerations (Architecture at 10 Million Tickets)

In a follow-up architecture review, scaling to 10+ million tickets requires specific database and query strategies:

1. **Covering Indexes & Filtering Performance**:
   - For high-cardinality searches, add covering composite indexes matching filter patterns (e.g. `(AssignedAgentId, Status) INCLUDE (Priority, DueAtUtc, CustomerId)`).
   - Full-text search on `Title` and `Reference` should transition from SQL `LIKE '%term%'` to SQL Server Full-Text Search or an external search index (Elasticsearch / Azure AI Search).
2. **Table Partitioning**:
   - Partition `Tickets` by `CreatedAtUtc` (e.g. quarterly or yearly partitions). Closed/archived historical tickets can be moved to cold storage or read-replicas, keeping active index trees compact in memory.
3. **Derived SLA Status at Scale**:
   - Dynamically evaluating SLA status in SQL via `CASE WHEN` date arithmetic on 10 million rows is performant when combined with indexed filter predicates (e.g. `Status IN (Open, InProgress)`).
   - Alternatively, a materialized persisted column or background projection table updated on state transitions (escalate, resolve) could be indexed directly.
4. **Keyset Pagination vs. Offset Paging**:
   - Replace `Skip(page * size).Take(size)` (which requires scanning discarded rows at high page numbers) with Keyset/Cursor-based pagination (`WHERE (CreatedAtUtc, Id) < (@lastCreatedAtUtc, @lastId) ORDER BY CreatedAtUtc DESC, Id DESC`).

---

## 5. Verification & Testing Philosophy (Automated + Manual)

High-integrity software delivery requires validating system behavior through two complementary pillars: automated specification tests and rigorous manual end-to-end exploratory testing.

### Automated Testing
- **Backend (110 Tests Passed)**:
  - `SupportDesk.UnitTests` (106 tests): Tested SLA calculation policies across all customer tiers and priorities (including the 1-hour floor), deterministic agent selection with tie-breakers, forced-Critical category rules, escalation commands, and dynamic SLA status exact boundaries.
  - `SupportDesk.ArchitectureTests` (4 tests): Enforced Clean Architecture layer boundaries using NetArchTest.
- **Frontend (29 Tests Passed)**:
  - Vitest + React Testing Library suites verifying the `DEFECT-117` regression fix, search input debouncing, `SlaIndicator` accessibility and glyph rendering, `EscalationPanel` validation and 409 error banners, and `ProtectedRoute` redirects.

### Manual Verification Performed Across All Tasks
Every single feature was exercised and verified manually in the running application:
1. **Task 1 (Defect & Server Filters)**:
   - Opened DevTools Network tab, changed the Status dropdown to *In progress*, and verified an immediate outgoing `GET /api/tickets` request was sent without touching search.
   - Verified search debounce: typed rapidly in the search input and confirmed no network requests fired per keystroke; a single request fired ~300ms after typing settled.
   - Tested exact filtering (`Priority=High`, `unassignedOnly=true`, customer name search, and combined criteria); inspected EF Core console logs to confirm pure SQL Server execution.
2. **Task 2 (Triage, Escalation & SLA)**:
   - Created tickets across various categories and customer tiers; validated automatic triage decisions (priority, SLA due date, agent assignment) on screen.
   - Tested ticket escalation on the detail page: confirmed validation on reasons (<5 characters blocked), verified single-level priority advancement, due date recalculation, and dynamic history refresh without page reload.
   - Tested escalation rejection on *Critical* and *Resolved* tickets: verified the button was disabled and direct API submission produced a clear HTTP 409 Conflict banner.
3. **Task 2.5 (SLA Summary Report)**:
   - Visited `/reports` dashboard: verified single-database aggregation queries, grouping toggles (*Agent* vs *Priority*), date filtering, and unassigned ticket tracking.
4. **Task 3 (Authentication & Protected Routes)**:
   - Tested login with valid agent credentials (`alex.turner@...` / `LocalDev123!`); verified JWT storage and automatic redirect back to intended page.
   - Tested invalid password and non-existent email: verified identical generic 401 error message with no user enumeration.
   - Tested login with inactive agent *Yuki Tanaka*: verified 401 rejection.
   - Tested protected route access while logged out: verified immediate redirect to `/login`; tested logout: verified session cleared.
5. **Task 4 (Containerisation & Docker Compose)**:
   - Ran `docker compose down -v && docker compose up --build -d` against an empty volume: verified SQL Server health check dependency, automatic EF Core migrations, and seeding on startup.
   - Verified `GET http://localhost:5080/health` reports Healthy without a token.
   - Tested React frontend proxy against the container (`VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`).
   - Verified least-privilege non-root execution (`docker compose exec api whoami` prints `app`).
   - Verified layer caching: edited a `.cs` file and observed cached NuGet restore during `docker compose build api`.
