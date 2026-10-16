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

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `docker compose up --build` | Run both SQL Server and the API containerized |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

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

## Task 3: Login and Authentication Documentation

### Design Decisions & Architecture
1. **Credential Storage on `Agent` Aggregate (`AU-1`)**:
   - In Northwind Support Desk, every authenticated user is an internal support agent (Alex, Ben, Priya, Sara, Marcus, Yuki).
   - Adding `PasswordHash` directly to `Agent` avoids unnecessary table joins and separate aggregate lifecycle synchronization. A committed EF Core migration (`AddAgentPasswordHash`) adds this column.
2. **Password Hashing & Salting (`AU-2`)**:
   - Uses standard PBKDF2 with HMAC-SHA256, 16-byte cryptographically secure random salt, and 100,000 iterations.
   - Password verification compares computed hash using `CryptographicOperations.FixedTimeEquals` to prevent timing side-channel attacks.
3. **Token Issuance & Lifetime (`AU-4`, `AU-7`)**:
   - JWT tokens are signed using HMAC-SHA256 with the key configured in `appsettings.Development.json` (and `.env.example`).
   - Token lifetime is set to **8 hours (480 minutes)** to cover a standard agent work shift without requiring mid-shift re-authentication.
4. **Central Authorization Enforcement (`AU-6`)**:
   - Central authorization is configured via ASP.NET Core `FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`.
   - Every existing and new endpoint requires a valid JWT by default without needing repetitive attributes.
   - Only `POST /api/auth/login` and Swagger UI are marked `[AllowAnonymous]`.
5. **Escalation Identity from Token (`AU-8`)**:
   - In `TicketsController.Escalate`, the `escalatedBy` field is extracted directly from the authenticated agent's JWT token (`User.Identity.Name`), eliminating client-side spoofing.
6. **Client Storage & Security Trade-offs (`AU-10`, `AU-13`)**:
   - Client stores the token in `localStorage` and automatically attaches it via `request()` in `apps/web/src/api/client.ts`.
   - Any `401 Unauthorized` response immediately clears the session and redirects the visitor to `/login`.
   - *Trade-off*: `localStorage` is convenient and survives page reloads and multiple tabs in this SPA, but is vulnerable to XSS. In a production environment with dedicated domains, `HttpOnly`, `SameSite=Strict`, `Secure` cookies paired with anti-CSRF protection or memory storage with rotating refresh tokens would be preferred.

### Development Login Credentials (`AU-3`)
| Agent | Email | Password | Status |
| :--- | :--- | :--- | :--- |
| **Alex Turner** | `alex.turner@northwind-support.example` | `LocalDev123!` | Active (Can log in) |
| **Ben Osei** | `ben.osei@northwind-support.example` | `LocalDev123!` | Active (Can log in) |
| **Priya Nair** | `priya.nair@northwind-support.example` | `LocalDev123!` | Active (Can log in) |
| **Sara Lindqvist** | `sara.lindqvist@northwind-support.example` | `LocalDev123!` | Active (Can log in) |
| **Marcus Doyle** | `marcus.doyle@northwind-support.example` | `LocalDev123!` | Active (Can log in) |
| **Yuki Tanaka** | `yuki.tanaka@northwind-support.example` | `LocalDev123!` | **Inactive** (Login rejected with 401 per AU-4) |

### Known Limitations (Out of Scope)
As specified in `ASSIGNMENT_CANDIDATE.md` (Section 3.4), the following features were deliberately not built:
- User registration / self-service sign up
- Password reset / "forgot password"
- Email verification
- OAuth / SSO / External identity providers
- Refresh tokens and rotating sessions
- Multi-factor authentication (MFA)
- Account lockout / IP rate limiting
- Complex role-permission matrices

---

## Task 4: Dockerise the API Documentation

### Overview & Architecture
Task 4 packages the .NET 10 API into an immutable, multi-stage Docker container (`apps/api/Dockerfile`) orchestratable alongside SQL Server via `docker-compose.yml`.

### Key Design & Implementation Decisions

1. **Multi-Stage Build & Layer Caching (`DK-1`)**:
   - **Build Stage**: Uses `mcr.microsoft.com/dotnet/sdk:10.0`. To maximize Docker layer cache hits, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and the 4 `.csproj` files (`Domain`, `Application`, `Infrastructure`, `Presentation`) are copied first.
   - `dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj` is executed in its own layer. Subsequent source code changes (`COPY apps/api/src/ apps/api/src/`) invalidate only the compilation and publish layers, reusing the cached restore layer (`CACHED`).
   - Published with `-c Release -o /app/publish --no-restore`.
2. **Context & Ignore Hygiene (`DK-2`)**:
   - Built from repository root as context (`build: context: . dockerfile: apps/api/Dockerfile`).
   - `.dockerignore` excludes `bin/`, `obj/`, `node_modules/`, `.git/`, `.env`, and `apps/web/` so no host artifacts or secrets enter the build context.
3. **Least Privilege Non-Root Execution (`DK-3`)**:
   - The runtime stage uses `mcr.microsoft.com/dotnet/aspnet:10.0` and switches to the built-in non-root user `USER $APP_UID` (UID `1654`, user `app`).
   - Listens on unprivileged HTTP port 8080 (`ASPNETCORE_HTTP_PORTS=8080`, `EXPOSE 8080`).
   - No certificates are baked into the image; TLS is terminated at a reverse proxy/ingress in front of the container in production.
4. **Zero Baked Secrets & Runtime Configuration (`DK-4`)**:
   - No connection strings, JWT keys, or credentials exist in the image or Dockerfile.
   - All configuration is injected via container environment variables in `docker-compose.yml`:
     - `ConnectionStrings__Default`
     - `Jwt__Key`
     - `ASPNETCORE_ENVIRONMENT=Development`
5. **Orchestration & Health Dependencies (`DK-5`)**:
   - In `docker-compose.yml`, the `api` service exposes `5080:8080` (accessible at `http://localhost:5080`).
   - Connects to SQL Server via its Docker service name `sqlserver` on port 1433 (`Server=sqlserver,1433`).
   - Starts only after SQL Server passes its health check (`depends_on: sqlserver: condition: service_healthy`).
6. **Central Health Check Endpoint (`DK-7`)**:
   - Implemented `DatabaseHealthCheck` in `SupportDesk.Presentation.Health`, verifying DB reachability via `SupportDbContext.Database.CanConnectAsync()`.
   - Exposed at `GET /health` with `app.MapHealthChecks("/health").AllowAnonymous();`.
   - Centrally configured as an exception to the Task 3 `FallbackPolicy`, responding `200 OK` ("Healthy") without requiring a JWT token.
7. **Frontend Proxy Integration (`DK-8`)**:
   - The React frontend dev server can proxy to the containerized API via:
     ```bash
     VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
     ```

### Verification Results (per Section 4.2)

- **Clean Volume Startup (`DK-6`)**:
  - Ran `docker compose down -v && docker compose up --build -d`.
  - SQL Server started, became healthy, and the API container applied all EF Core migrations and seeded 40 tickets, 6 customers, 5 categories, and 6 agents on an empty volume.
  - `GET http://localhost:5080/health` returns `200 OK` (`Healthy`).
  - `POST http://localhost:5080/api/auth/login` and `GET http://localhost:5080/api/tickets` return the 40 seeded tickets with JWT authentication.
- **Restore Layer Caching**:
  - Modifying a `.cs` file and rebuilding (`docker compose build api`) reuses the cached restore layer (`RUN dotnet restore ... CACHED`), rebuilding in ~4 seconds.
- **Image Size Comparison**:
  - SDK image (`mcr.microsoft.com/dotnet/sdk:10.0`): **1.3 GB**
  - Runtime base image (`mcr.microsoft.com/dotnet/aspnet:10.0`): **340 MB**
  - Published API image (`northwind-support-api:latest`): **455 MB**
  - **Savings**: ~845 MB reduction (65% smaller than the SDK image).
- **Non-Root Execution**:
  - `docker compose exec api whoami` prints `app`.
  - `docker compose exec api id` prints `uid=1654(app) gid=1654(app) groups=1654(app)`.

### Production Database Migration Strategy

In local development, the API applies EF Core migrations and demo seeds at startup via `await app.MigrateAndSeedAsync()` when `ASPNETCORE_ENVIRONMENT=Development`. 

**Why this is NOT done in production:**
1. **Concurrency & Race Conditions**: In production environments with multiple API replicas (e.g. horizontally autoscaled pods in Kubernetes or ECS tasks), replicas starting concurrently will race to apply migrations simultaneously, leading to lock contention, migration failures, or corrupted migration history tables.
2. **Principle of Least Privilege**: Running migrations on application startup requires the API runtime connection string to possess DDL privileges (`CREATE TABLE`, `ALTER TABLE`, `DROP INDEX`). In production, the running API should only have DML permissions (`SELECT`, `INSERT`, `UPDATE`, `DELETE`).
3. **Deployment Safety & Zero-Downtime Rollouts**: If a migration fails during application startup, container instances crash-loop, disrupting existing traffic.

**Production Recommendation:**
- **CI/CD Pipeline Deployment**: Generate idempotent SQL migration scripts at build time via `dotnet ef migrations script --idempotent` and apply them in a controlled deployment pipeline with DBA review and rollback plans.
- **Migration InitContainer / Bundle**: If automated during deployment, run a single-instance Kubernetes Job or `initContainer` using an EF Core Migration Bundle (`dotnet ef migrations bundle`) that executes with elevated DDL privileges, verifies success, and terminates before live API replicas are rolled out.

### Known Limitations (Out of Scope)
As specified in `ASSIGNMENT_CANDIDATE.md` (Section 4.3):
- Kubernetes manifests / Helm charts
- Image pushing to external container registries (e.g., ACR / ECR)
- CI/CD container pipeline configuration
- In-container HTTPS / TLS certificates (terminated at edge/reverse proxy)
- Dockerising the React web application
- Docker secrets / secret vaults (Vault, AWS Secrets Manager)
- Multi-architecture container builds (ARM64 cross-compilation)
