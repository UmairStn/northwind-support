# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker Desktop.

### Option A: Local Development (Default)
Run SQL Server in Docker, then run the API and Web client locally:
```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # install web dependencies
npm run db:up                 # terminal 1: SQL Server in Docker on localhost:1433
npm run api                   # terminal 2: API (migrates, seeds, serves on localhost:7043/5043)
npm run web                   # terminal 3: React dev server on http://localhost:5173
```

### Option B: Running with Docker (Containerized Backend)
Run the full backend (SQL Server + containerized API) using Docker Compose (Task 4):
```bash
# Start both SQL Server and the API container in the background
docker compose up -d --build

# Run the React client pointing to the containerized API (localhost:5080)
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

| Service | Address | Notes |
| --- | --- | --- |
| **Web app** | http://localhost:5173 | React 19 SPA |
| **API (Local)** | https://localhost:7043 / http://localhost:5043 | Local .NET dev server |
| **API (Docker)** | http://localhost:5080 | Containerized API (`docker compose up`) |
| **Swagger UI** | https://localhost:7043/swagger or http://localhost:5080/swagger | Open without auth |
| **Health Check** | http://localhost:5080/health | Database connectivity probe |

In Development the API automatically applies migrations and seeds demo data on startup.

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set **SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # Run both test suites (110 .NET tests + 29 Vitest tests)
dotnet test                   # Run backend tests only
npm run test --workspace apps/web  # Run frontend tests only
```

Both pass on a clean checkout and neither needs a running database.

## Commands

| Command | What it does |
| --- | --- |
| `docker compose up -d --build` | Build and start SQL Server and API containers in background |
| `docker compose down -v` | Stop containers and wipe the database volume |
| `docker compose logs -f api` | Stream logs from the containerized API service |
| `docker compose exec api whoami` | Verify the container process executes as non-root user `app` |
| `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web` | Run React dev server against the containerized API |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe standalone SQL Server container |
| `npm run api` / `npm run web` | Run the local .NET API / the React dev server |
| `npm test` | Run all automated tests (backend + frontend) |
| `npm run build` | Build both the .NET solution and the React client bundle |
| `npm run lint` / `typecheck` | Run ESLint and TypeScript strict typechecks (`tsc -b`) |
| `npm run format` / `format:check` | Run `dotnet format` to apply or check code formatting |
| `npm run ef -- migrations add <Name>` | Add a new EF Core migration |
| `npm run db:migrate` | Apply EF Core migrations to local database |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /health` | Health check reporting database connectivity (open without token) |
| `POST /api/auth/login` | Sign in with email and password, returning JWT token |
| `GET /api/auth/me` | Current authenticated agent details |
| `GET /api/tickets` | Paged list with search, status, priority, category, customer, agent, SLA status |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket with automated priority, SLA due date, and agent triage |
| `POST /api/tickets/{id}/escalate` | Escalate ticket priority and re-evaluate owner |
| `GET /api/tickets/{id}/escalations` | Escalation history for a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |
| `GET /api/reports/sla-summary` | SLA performance report aggregated in SQL by `agent` or `priority` (`from`, `to`) |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |

---

## Candidate Implementation & Deliverables

### Development Login Credentials (`AU-3`)
All seed agent accounts use the local fixture password `LocalDev123!`:
- **Active Agents (Can log in)**: `alex.turner@northwind-support.example`, `ben.osei@northwind-support.example`, `priya.nair@northwind-support.example`, `sara.lindqvist@northwind-support.example`, `marcus.doyle@northwind-support.example`
- **Inactive Agent (Login rejected with 401)**: `yuki.tanaka@northwind-support.example`

### Task 1: Defect Fix & Server-Side Filtering
- **DEFECT-117 Root Cause & Fix**: In `apps/web/src/features/tickets/hooks/useTicketList.ts`, filter dropdown updates were coupled with search input state in a single debounced flow. Decoupled them so dropdown changes immediately push query parameters and dispatch `GET /api/tickets`, while search typing remains debounced (300ms) with no request-per-keystroke.
- **Verification**: Regression test in `useTicketList.test.ts` asserts immediate request dispatch on filter change. Manually verified with browser DevTools Network tab.
- **Server-Side Filtering (`FL-1`–`FL-6`)**: Implemented in `TicketFilters.ApplyFilters` on `IQueryable<Ticket>`. Evaluates exact matches (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`), `unassignedOnly`, and trimmed `search` (matching title, reference `TCK-0042`, or customer name) with logical `AND`.
- **SQL Execution & Pagination**: Filters execute directly in SQL Server before `CountAsync()` and `Skip/Take` paging (verified via EF Core console logs). Adding Task 2's `slaStatus` was done as a single composable predicate without restructuring the query pipeline.

### Task 2: Ticket Escalation, Assignment & SLA Report
- **Automated Triage (`BR-1`–`BR-6`)**:
  - `PriorityPolicy`: Category flagged `ForcesCriticalPriority` (Security, Outage) forces `Critical`; otherwise honors requested priority (default Medium).
  - `SlaPolicy`: Base SLA windows (Critical 4h, High 8h, Medium 24h, Low 72h) halved for Premium customers with a 1-hour minimum floor. Configured via `IOptions<SlaOptions>`.
  - `AgentAssignmentService`: Assigns to active specialists strictly below `MaxOpenTickets` limit with fewest open tickets (open = not Resolved/Closed), breaking ties deterministically by lowest agent `Id`. Returns HTTP 201 unassigned with human-readable reason when no specialist is eligible.
- **Escalation (`BR-7`)**: Advances priority one step (`Low` → `Medium` → `High` → `Critical`), recomputes due date from `now`, re-evaluates owner, requires 5–500 char reason, and records immutable row in `TicketEscalations`. Rejects Critical or Resolved/Closed tickets with HTTP 409 Conflict.
- **Derived SLA Status (`BR-8`)**: Evaluated dynamically (`WithinSla`, `AtRisk`, `Breached`, `Met`, `NotApplicable`) using injectable `ISystemClock` without storing state in DB.
- **Database Indexing**: Committed migration `AddTicketEscalations`. Configured composite descending index `IX_TicketEscalations_TicketId_EscalatedAtUtc` (`TicketId ASC, EscalatedAtUtc DESC`) enabling index seek without sorting for newest-first history retrieval.
- **Frontend Components**: Reusable `SlaIndicator` (accessible glyphs + labels, not color alone), escalation panel with validation and 409 conflict handling, and create triage breakdown.
- **Task 2.5 Stretch (SLA Report)**: `GET /api/reports/sla-summary?from=&to=&groupBy=agent|priority` executes as a single SQL Server `GroupBy` aggregation using `COUNT(*)`, conditional `SUM(CASE WHEN ...)`, and `AVG(DATEDIFF)` with a left join to preserve unassigned tickets. Rendered in a dedicated `/reports` UI dashboard.

### Task 3: Login and Authentication
- **Architecture & Credentials (`AU-1`, `AU-2`)**: `PasswordHash` stored directly on `Agent` aggregate via migration `AddAgentPasswordHash` (since all users are agents, avoiding unnecessary joins). Passwords hashed using PBKDF2 with HMAC-SHA256, 16-byte random salt, and 100,000 iterations via `Rfc2898DeriveBytes`. Constant-time equality check via `CryptographicOperations.FixedTimeEquals`.
- **Endpoints & Central Policy (`AU-4`–`AU-7`)**: `POST /api/auth/login` returns JWT with 8-hour lifetime (480 min). Wrong email/password return identical generic 401. ASP.NET Core `FallbackPolicy` centrally enforces authentication across all endpoints by default; only `login`, `/health`, and Swagger are `AllowAnonymous`.
- **Actor Integrity (`AU-8`)**: `escalatedBy` is extracted directly from JWT claim `User.Identity.Name`, preventing client-side spoofing.
- **Frontend Session (`AU-9`–`AU-13`)**: Accessible `/login` page with field validation and demo credentials helper. JWT stored in `localStorage` and attached via `client.ts`. Protected routes redirect unauthenticated visitors to `/login` and return them on success. Any 401 mid-session immediately clears storage and redirects to `/login`.
  - *Trade-off*: `localStorage` survives page reloads in this SPA; in production, `HttpOnly`, `SameSite=Strict`, `Secure` cookies with anti-CSRF tokens would be preferred to mitigate XSS risks.

### Task 4: Dockerise the API
- **Multi-Stage Build (`DK-1`, DK-2`)**: `apps/api/Dockerfile` uses `mcr.microsoft.com/dotnet/sdk:10.0` for restore/build and `aspnet:10.0` runtime. Copies `.csproj`, `Directory.Build.props`, and `Directory.Packages.props` first; `dotnet restore` is cached across code changes. `.dockerignore` excludes `bin`, `obj`, `node_modules`, `.git`, `.env`, and web app.
- **Least Privilege & Security (`DK-3`, `DK-4`)**: Runs as non-root user `USER $APP_UID` (`app`, UID 1654) on HTTP port 8080. Zero secrets baked into image; connection strings, JWT keys, and environment variables are injected at runtime via `docker-compose.yml` / `.env.example`.
- **Orchestration & Health (`DK-5`–`DK-8`)**: `api` service maps `5080:8080`, waits for SQL Server `service_healthy`, and applies EF migrations and demo seeds on empty volumes. `GET /health` probes database reachability without authentication. React app proxies via `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`.
- **Verification Results**:
  - Image size: **455 MB** runtime image vs **1.3 GB** SDK image (~65% reduction).
  - Cached restore: Rebuilding after a `.cs` change reuses `CACHED` restore layer in ~4s.
  - Non-root: `docker compose exec api whoami` prints `app` (`uid=1654`).
- **Production Migration Strategy**: In development, `MigrateAndSeedAsync()` runs at startup. In production, migrations must run outside the API process (via CI/CD idempotent scripts `dotnet ef migrations script --idempotent` or a Kubernetes initContainer / Migration Bundle) to prevent concurrency race conditions across autoscaled replicas and adhere to least-privilege principles (runtime service account needs DML only, not DDL).

### Testing Strategy (Automated & Manual Verification)
- **Automated Test Coverage (139 Tests Passing)**:
  - `dotnet test` (110 tests): 106 unit tests (SLA windows, tier halving with 1h floor, specialist selection, deterministic tie-breakers, 409 rejections, boundary checks) and 4 Clean Architecture layer enforcement tests.
  - Vitest (29 tests): Covers `DEFECT-117` regression, search debouncing, accessible `SlaIndicator` rendering across all states, `EscalationPanel` validation and 409 conflict handling, and `ProtectedRoute` redirection.
  - *Deliberately Not Tested*: Framework plumbing (built-in routing, EF internal SQL execution) and live SQL Server integration during unit runs (SQLite in-memory test doubles and Moq used for deterministic speed).
- **Manual End-to-End Verification Across All Tasks**:
  - Verified `DEFECT-117` and server-side filtering via browser DevTools Network tab and EF Core console SQL logs.
  - Raised tickets verifying automatic triage (priority forcing, halved SLA, specialist assignment) and tested detail page escalation with 409 conflict handling.
  - Verified `/reports` aggregation dashboard and grouping toggles.
  - Tested `/login`, invalid credential rejection, inactive agent rejection, route guarding, and logout.
  - Executed `docker compose down -v && docker compose up --build`, verifying `/health`, non-root execution (`app`), and frontend proxying.

### AI Usage Account
- **Tools Used**: Google Antigravity / Gemini 3.8 Flash for boilerplate CQRS scaffolding, edge-case unit test generation, EF Core SQL expression translation, and Docker layer caching.
- **Workflow (Requirements-Led TDD)**: After analyzing the assignment requirements, we specifically formulated and provided the precise domain rules, business constraints (`BR-1`–`BR-8`), and edge-case boundary scenarios (such as the 1-hour floor for halved windows and deterministic ID tie-breaks), directing the AI to generate failing xUnit test suites *before* implementing domain logic.
- **Accepted As-Is**: PBKDF2 password hasher with constant-time equality check; multi-stage Dockerfile layer restore caching.
- **Changed / Rejected**: Rejected in-memory SLA filtering in favor of pure SQL expression trees in `TicketFilters.cs` to satisfy `FL-5`; corrected triage DTO property name to `slaReason`; removed ambiguous DI constructor on `SlaPolicy`.
- **Bugs AI Helped Find / Introduced**: Helped isolate entangled filter state in `useTicketList.ts`; caught non-composite index on `TicketEscalations` and replaced it with composite descending index.
- **Verification**: All AI-assisted code was validated via 139 passing automated tests, `tsc -b --noEmit`, ESLint, SQL console inspection, and clean-volume Docker boots.

### Known Limitations (Out of Scope)
As permitted by specification: No user self-registration, password reset, OAuth/SSO, refresh tokens, MFA, or complex role matrices. In production, JWT storage would transition from `localStorage` to `HttpOnly`, `SameSite=Strict` cookies with anti-CSRF protection, and migrations would run via CI/CD release pipelines.
