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
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. **Filters are accepted but not applied yet** (Task 1). |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

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
