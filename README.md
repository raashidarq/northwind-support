# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## Submission summary

Requirements: [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md). My approach and thought process for
each task: [APPROACH.md](APPROACH.md).

| Task | Status |
| --- | --- |
| 1. Defect fix + server-side filtering | Done |
| 2. Ticket escalation & assignment | Done, including the 2.5 SLA summary report |
| 3. Login and authentication | Done |
| 4. Dockerise the API | Done |

## What was implemented and the rules as understood

- **DEFECT-117:** every filter change on the ticket list now sends a request straight away; only the
  search box waits for typing to stop (300 ms).
- **Server-side filtering:** every list filter (search, status, priority, category, customer,
  assigned agent, unassigned only) is applied in the database, and the total count matches the
  filtered list.
- **Triage on create (BR-1 to BR-6):** `POST /api/tickets` sets the priority, due date and owner, and
  returns a `triage` object with a reason for each. No eligible agent still gives 201, unassigned.
- **Escalation (BR-7):** `POST /api/tickets/{id}/escalate` and `GET /api/tickets/{id}/escalations`,
  with a `TicketEscalations` history table.
- **SLA status (BR-8):** *AtRisk* added, a `slaStatus` filter applied in SQL, and in the UI one SLA
  indicator, an SLA filter, an escalation panel with history, and the triage result after create.

Where the brief left a gap, I decided:
- The 1-hour floor applies to every window, not only Premium ones (it only bites when the config is
  changed).
- Agent tie-break: lowest agent id. Stable and predictable.
- *AtRisk* includes both ends: exactly 25% left is at risk, and so is exactly the due time. Breached
  starts strictly after it.
- On escalation an agent exactly at their limit **keeps** the ticket: it is already counted in their
  load, so keeping it adds nothing. They would not be picked for a *new* ticket.
- Reasons are trimmed and measured after trimming (5-500), on the server and in the UI.

- **Login (Task 3):** agents sign in with email and password; every API endpoint except login needs
  their token; the web app has a login page, sends the token with every request, shows who is
  signed in with a Sign out button, and returns to login on any 401. The escalation's
  `escalatedBy` is now the signed-in agent, taken from the token.

## Design decisions

**DEFECT-117: split "debounce the typing" from "fetch when filters change".**
- *Chosen:* the search box's live text is kept in its own state (`searchText`). A small effect copies
  it into `filters.search` 300 ms after typing stops. A second effect fetches whenever `filters`
  changes, depending on the whole object. Any filter change therefore requests immediately, and a new
  filter (Task 2's `slaStatus`) needs no extra wiring because it is just another field on `filters`.
- *Rejected:* adding each field to the old dependency list. Dropdowns would still wait for the
  debounce (not "immediate"), and every new filter would need adding to the list by hand.
- *Kept as-is:* one `react-hooks/set-state-in-effect` suppression on the fetch effect. Fetching is
  that effect's purpose and `load` must set `isLoading` first; the reason is written beside it.
  The old `exhaustive-deps` suppression was removed, so the linter now checks the dependency list.

**Filtering: one `Where` per filter in the existing query class.**
- *Chosen:* a private `ApplyFilters` in `TicketQueries`, beside the existing `ApplySort`, applied
  before the count.
- *Decided:* if both `assignedAgentId` and `unassignedOnly` are sent, both apply, so nothing matches.
  The UI never sends both.

**Task 2: the rules live in the domain; the handlers only gather their inputs.**
- *Chosen:* `PriorityRule`, `SlaPolicy`, `AgentSelector` and `SlaEvaluator` are small classes in
  `SupportDesk.Domain` with no database or HTTP, so each is tested on its own. Escalation is a method
  on the aggregate, `Ticket.Escalate`, which refuses Critical, Resolved and Closed tickets (→ 409),
  the same way the existing `ChangeStatus` refuses closed tickets. The architecture tests stop the
  domain depending on anything.
- *Rejected:* rules in the command handlers. They would need mocks to test, and the create and
  escalate handlers would each hold a copy of the SLA calculation.
- *Configuration:* the numbers come from the existing `Sla` section, bound once to `SlaPolicy` in
  `Program.cs`. Category behaviour comes only from the `ForcesCriticalPriority` and
  `RequiresSpecialist` flags; no category names appear in code.
- *AgentCandidate:* the picker works on a small record (id, name, active, limit, specialisms, open
  count) filled by one database query, not on `Agent` entities. That keeps the rule easy to test with
  any ids, and the open-ticket counts are counted in SQL.

**AtRisk needs the length of the current window.**
- *Chosen:* derive it from the ticket's current priority and customer tier via `SlaPolicy`. That is
  correct after an escalation too, because escalation sets the new priority and a due date from
  exactly that window. No new column.
- *Rejected:* `DueAtUtc - CreatedAtUtc` (wrong once a ticket has been escalated); a stored
  `SlaStartedAtUtc` column (exact, but another column, seeding and harder SQL for little gain).
- *Trade-off:* if the config changes, existing tickets' at-risk point moves with it.

**SLA filter in SQL.** The C# evaluator cannot run in SQL, so each status is rewritten as plain date
comparisons. "At risk" depends on priority and tier, so the 8 possible cut-off times (now + at-risk
period) are worked out in C# and SQL picks one per row with a `CASE`. No date arithmetic happens in
SQL, so the same query runs on SQL Server and on SQLite in the tests. The list's displayed status and
the filter use the same "now", so they can't disagree.

**Triage reasons** are returned as a `triage` property on the existing ticket response, filled in
only by create, rather than a second response type.

**Task 3: credentials on `Agent`, not a separate Users table.**
- *Chosen:* agents already had a unique `Email`, so one nullable `PasswordHash` column was enough.
  Only agents sign in, and the brief rules out registration and roles.
- *Rejected:* a `Users` table linked to `Agent`, or full ASP.NET Core Identity. Both add tables and
  concepts (roles, claims, lockout) the brief says are out of scope.
- *Secure by default:* a fallback authorization policy makes every endpoint require a signed-in
  agent; only login is `[AllowAnonymous]`. A new endpoint is protected without anyone remembering.
- *Layers:* the Application layer only sees `IPasswordHasher` and `ITokenIssuer`; the
  implementations (Identity's hasher, JWT) live in Presentation, which already references ASP.NET
  Core, so the architecture tests still pass and no package was added for hashing.

## The defect (DEFECT-117)

**Root cause.** The effect in `useTicketList` that loads tickets only re-ran when `search`, `page` or
`pageSize` changed. The linter's missing-dependency warning had been switched off on the mistaken
belief that the `filters` object changes identity every render (it doesn't: React returns the same
state object until it is set). So changing Status, Priority or any other dropdown updated state but
sent no request; the new value only went out with the next search or page change.

**Fix.** Separate the two jobs the effect was doing: debounce the search text into `filters`, and fetch
whenever `filters` changes. Commits `4f34380` (fix) and `8186a82` (tests).

**How to verify.**
1. *Tests:* `npm run test --workspace apps/web`. `useTicketList.test.ts` checks, for each of the seven
   filters, that one change sends one request immediately with the new value, and that three quick
   keystrokes send a single request that keeps the other filters. Against the code before the fix,
   the seven filter tests fail (0 requests sent); after the fix all pass.
2. *By hand:* open the ticket list, press F12 → Network, filter by `tickets`. Change Status to
   *In progress*: a request with `status=InProgress` appears at once. Type in Search: one request after
   typing stops, still including the status.

## Filtering

**Where.** `TicketQueries.GetPagedAsync` (Infrastructure) calls a private `ApplyFilters` method before
it counts. Each filter that was sent adds one `Where`; filters left out add nothing. Nothing runs until
`CountAsync` / `ToListAsync`, when EF Core turns all the `Where`s into a single SQL `WHERE ... AND ...`.
The database does the filtering, the count, the sorting and the paging, and the count is taken after
filtering so `totalCount` and the page numbers match the filtered list. Commit `fe87c03`.

**Search.** Trimmed; blank or whitespace-only search is ignored. It matches if the ticket title,
reference or customer name *contains* the text. It is case-insensitive because of SQL Server's default
collation, not because of the code.

**How the SQL was checked.** In development, EF Core logs every SQL command in the API terminal. After
picking Priority *High* in the app, the `SELECT COUNT(*)` and the paged `SELECT` both carried
`WHERE [t].[Priority] = @priority`.

**Adding the next filter.** Add the property to `TicketQuery` (bound from the query string
automatically), add one `if (...) source = source.Where(...)` in `ApplyFilters`, and add a test in
`TicketQueriesTests`. Task 2's `slaStatus` was the exception: see "SLA filter in SQL" under Design
decisions. Checked the same way: across the five SLA filters the 40 seeded tickets came back
14 + 5 + 9 + 12 + 0 = 40, each once, and the log shows the `WHERE ... CASE ...` on both the count
and the page.

## Authentication

- **Credential storage:** `Agents.PasswordHash` (nullable; an agent without one cannot sign in).
  Plain passwords are never stored or logged.
- **Hashing:** ASP.NET Core Identity's `PasswordHasher`: PBKDF2 with HMAC-SHA512, a random salt
  per password and many iterations, and the settings are stored inside the hash so they can be
  raised later without breaking old hashes.
- **Login:** `POST /api/auth/login` with `{ email, password }`. Unknown email, wrong password and
  inactive agent all return the same 401 "Email or password is incorrect.". An unknown email still
  costs one hash, so the response time does not reveal which emails exist.
  `GET /api/auth/me` returns the signed-in agent, re-read so a deactivated agent is refused.
- **Token issue:** a JWT signed with HMAC-SHA256, carrying the agent's id (`sub`), name and email,
  checked for issuer, audience, signature and expiry. The signing key comes only from configuration:
  a dev-only key in `appsettings.Development.json` (like the existing dev database password), and
  `Jwt__Key` everywhere else (see `.env.example`). The API will not start without a key of at
  least 32 characters.
- **Token storage (web):** `sessionStorage`, so it survives a reload but not closing the tab. The
  shared `request` function adds `Authorization: Bearer ...` to every call and, on any 401, clears
  the session; the route guard then sends the agent to `/login?returnTo=...` and back after sign-in.
- **Lifetime:** 60 minutes (`Jwt:LifetimeMinutes`). No refresh token (out of scope), so the agent
  signs in again after an hour.
- **For production I would change:** a real secret in a secret store (Key Vault or similar),
  rotated; an `HttpOnly`, `Secure`, `SameSite` cookie instead of `sessionStorage`, so a script
  injected into the page cannot read the token; HTTPS only; rate limiting and lockout on login;
  shorter tokens with refresh tokens; a way to revoke tokens (e.g. on deactivation).

## Development login credentials

Every active seeded agent signs in with the password **`Northwind-Dev-2026`**
(`DevSeed:AgentPassword` in `appsettings.Development.json`; development seed data only):

| Agent | Email |
| --- | --- |
| Alex Turner | `alex.turner@northwind-support.example` |
| Ben Osei | `ben.osei@northwind-support.example` |
| Priya Nair | `priya.nair@northwind-support.example` |
| Sara Lindqvist | `sara.lindqvist@northwind-support.example` |
| Marcus Doyle | `marcus.doyle@northwind-support.example` |

Yuki Tanaka (`yuki.tanaka@northwind-support.example`) is inactive, so that sign-in is refused.
The seeder also sets these passwords on a database seeded before login existed.

## SLA summary report (2.5)

`GET /api/reports/sla-summary?groupBy=agent|priority&from=&to=` (signed-in agents only) returns one
row per agent or per priority for tickets created in [from, to). Both dates are optional and are
read as UTC.
- **Each row:** total tickets; counts of breached, at risk, within SLA and met; breach rate; average
  resolution hours.
- **Counts:** the four SLA counts use exactly the same rules as the list's SLA status and filter
  (the cut-offs are shared code), and leave out tickets with no due date, so they can add up to less
  than the total.
- **Breach rate:** breached ÷ tickets that have a due date (tickets with no SLA cannot breach); null,
  not 0, when a group has none, because 0% would suggest "no breaches" rather than "nothing to
  measure".
- **Average resolution hours:** created → resolved, over resolved tickets only; null when none are.
- **Groups:** grouping by agent keys on agent id and name, with unassigned tickets as "Unassigned"
  (listed last). Grouping by priority lists Critical first.
- **400** for a `groupBy` other than agent or priority, or a `to` that is not after `from`.

**One query.** Each ticket's SLA status is a SQL `CASE`. Each group is then counted with
conditional `COUNT(CASE ...)`, plus `AVG(DATEDIFF(second, ...))`, all under one `GROUP BY`. No
tickets are loaded into memory; only the breach rate and rounding are done in C#, on the few group
rows.

**Tests** (in-memory SQLite): counts per SLA state, the Unassigned group, both edges of the date
range, the average, validation, and a check that exactly one SQL command containing `GROUP BY` is
executed. SQLite has no `DATEDIFF`, so the test project maps it to a `julianday` difference
(`SqliteDateDiffTranslatorProvider`, test code only) and the average is tested too.

**Checked on SQL Server:** grouped by priority and then by agent, the counts summed to 9 breached,
5 at risk, 14 within and 12 met, exactly what the list's SLA filter returned on the same database,
and to 40 tickets in total.

## Docker

**Build and run** (from the repository root; needs only Docker):

```bash
docker compose up --build          # SQL Server, then the API once SQL Server is healthy
```

- API: http://localhost:5080 (Swagger at /swagger in Development), health at http://localhost:5080/health.
- On an empty volume the API applies the migrations and seeds the demo data, so the dev logins work.
- To use the web app against the container:
  `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`
  (PowerShell: `$env:VITE_API_PROXY_TARGET="http://localhost:5080"; npm run web`).
- `npm run db:up` still starts SQL Server alone, for running the API with `npm run api`.

**The image** (`apps/api/Dockerfile`, built from the repository root):
- Two stages: the .NET 10 SDK builds and publishes; only the published output is copied into the
  much smaller ASP.NET runtime image. No SDK and no source code in the final image.
- Restore is its own layer: `global.json`, `Directory.Build.props`, `Directory.Packages.props`
  and the four `.csproj` files are copied and restored before any source, so changing a `.cs` file
  reuses the cached restore.
- Runs as the image's built-in non-root `app` user, on plain HTTP port 8080. No certificates.
- `.dockerignore` keeps out `bin/`, `obj/`, `node_modules/`, `.git/`, `.env`, the web app, the
  tests, and `appsettings.Development.json` (it holds the dev connection string and dev JWT key).

**Environment variables** (set in `docker-compose.yml`; override in `.env`, see `.env.example`):

| Variable | Purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` (`API_ENVIRONMENT`) | `Development` migrates and seeds on start-up and enables Swagger |
| `ConnectionStrings__Default` | SQL Server, reached by its compose service name `sqlserver` |
| `Jwt__Key` (`JWT_KEY`) | Token signing key, at least 32 characters |
| `DevSeed__AgentPassword` (`DEV_AGENT_PASSWORD`) | Password given to the seeded agents |

The compose file has throwaway local defaults for these, in the same way it already did for the SA
password, so `docker compose up --build` works with no `.env`. Nothing is baked into the image.
The SLA numbers moved from `appsettings.Development.json` to `appsettings.json`: they are
business settings, not secrets, and every environment (including this container) needs them.

**`GET /health`** checks that the database can be reached: 200 `Healthy` or 503 `Unhealthy`. It is
mapped once in `Program.cs` and is open without a token.

**Image size:** the runtime image is **457 MB**; the SDK build stage it is made from is **2.42 GB** (SDK, restored packages and source), so the image that ships is about a fifth of the size.

**Migrations in a real deployment.** Migrating on start-up is a development convenience. In
production I would not let the app change its own schema: several instances starting at once would
race to migrate, the app would need permission to alter tables, and a failed migration would surface
as a crashing app. Instead I would generate an idempotent SQL script
(`dotnet ef migrations script --idempotent`) or a migration bundle (`dotnet ef migrations bundle`),
review it, and run it as a separate step in the release pipeline before the new version starts,
with a database user that is allowed to change the schema while the app's own user is not.

**How each check was done** (29 October, Docker Desktop 29.8):
- `docker compose down -v && docker compose up --build`: on the empty volume the API migrated and
  seeded; `/health` returned 200 `Healthy` without a token, `/api/tickets` returned 401 without a
  token and `totalCount: 40` after signing in as a seeded agent.
- Added a comment to one `.cs` file and rebuilt with `--progress=plain`: the `dotnet restore` step
  was `CACHED`; only copying the source and `dotnet publish` ran again.
- `docker images`: 457 MB for the runtime image vs 2.42 GB for the build stage
  (`docker build --target build`).
- `docker compose exec api whoami` printed `app`. `ls /app` in the container shows only
  `appsettings.json` (no Development settings) and no source files.
- `VITE_API_PROXY_TARGET=http://localhost:5080` with the Vite dev server: signing in through the
  proxy reached the container (200).

## Database

Added by the migration `AddTicketEscalations` (committed in `Data/Migrations`), applied on start-up in
Development:
- **`TicketEscalations`**: Id, TicketId, From/To Priority (with check constraints like `Tickets`),
  From/To AgentId (nullable), From/To DueAtUtc (`datetime2(3)` like the other dates; From nullable
  because a ticket could have had no due date), Reason (500), EscalatedBy (100), EscalatedAtUtc.
- **Delete behaviour:** history is part of the ticket, so deleting a ticket deletes its history
  (cascade). Agents are restricted: an agent with history cannot be deleted, so history never points
  at a missing agent.
- **Index `IX_TicketEscalations_TicketId_EscalatedAtUtc`**: serves the history endpoint exactly (one
  ticket's rows, newest first) without a sort step, and also covers the foreign key. EF Core adds
  its default indexes on the two agent foreign keys.
- **`Agents.PasswordHash`** (`nvarchar(256)`, nullable): added by the migration
  `AddAgentPasswordHash` for Task 3. No index: agents are looked up by `Email`, which already has a
  unique index.
- No other tables changed. Escalation rows are never updated: the entity has private setters and no
  methods that change it.

## Testing

- Run everything: `npm test` (backend xUnit, then web Vitest). No database needed.
- Baseline before any change: 30 backend + 2 web tests, all passing. Now 113 backend unit + 4 architecture + 23 web.
- **DEFECT-117** (`apps/web/src/features/tickets/hooks/useTicketList.test.ts`): 8 tests on the hook
  with the API mocked and timers faked, asserting which request is sent and when.
- **Filtering** (`apps/api/tests/SupportDesk.UnitTests/Infrastructure/Queries/TicketQueriesTests.cs`):
  6 tests that run the real query against an in-memory SQLite database (not a C# list), so they prove
  the filters translate to SQL. They cover priority, several filters combined, customer-name search
  with surrounding spaces, blank search, unassigned only, and the total count when the page is smaller
  than the result. 5 of 6 fail on the old code (blank search passes, because ignoring it is what the
  old code did anyway).
- **Task 2 rules** (`Domain/...`): SLA windows for every priority × tier and the 1 h floor;
  forced-Critical categories; agent selection (fewest open, specialists, limits, inactive, ties,
  nobody eligible, keep-or-reassign on escalation); escalation (one step, due date from now, owner,
  the history row, Critical / Resolved / Closed rejected); SLA status at exact boundaries.
- **Task 2 handlers and validator:** triage on create (due date, forced Critical, assignment, nobody
  eligible still saves), escalate (saves only on success, 404, 409), reason length after trimming.
- **SLA filter and history** (SQLite): every SLA state including boundaries and the Premium/Standard
  difference, each result also checked against `SlaEvaluator`; history newest first with agent
  names. Breaking the at-risk boundary (`<=` → `<`) makes the test fail.
- **Web:** short reason blocks submit and never calls the API; a 409 is shown and the ticket is
  unchanged; the escalate button is disabled for a Critical ticket; every SLA state has its own text
  and colour.
- **Login (backend):** the right password issues a token; wrong email and wrong password fail with
  the identical message; an unknown email still costs a hash; an inactive agent gets no token.
- **Login (web):** the token is sent with every request; a 401 clears the session; the login form
  checks its fields before calling the API; a wrong password shows the server's message; a
  successful sign-in returns to the page the agent wanted.
- Each endpoint was also called against SQL Server (200 / 400 / 404 / 409, history, triage).
- *Not tested:* search case-insensitivity. SQLite compares case-sensitively and SQL Server, by
  default, does not, so a test here would only be testing SQLite. Two tickets created at the same
  moment racing for the last slot of an agent's limit (see Known limitations). Login has no automated
  end-to-end test of "every endpoint is protected"; I checked it with curl instead: no token, a
  tampered token, and each failed-login case all return 401.

## AI usage

- **Tool:** Claude Code (Claude Opus), used as a pair programmer and tutor.
- **Purpose:** checking the dev environment; explaining React hooks, debouncing, EF Core and the
  brief's terms; writing the regression tests, the fix and the filtering after I had reproduced the
  bug and agreed the root cause.
- **Workflow that worked:** reproduce by hand in the Network tab → agree the root cause → write the
  test and watch it fail → fix → watch it pass → check again in the browser (or in the SQL log).
- **Accepted as-is:** the shape of `ApplyFilters`, because it follows the existing `ApplySort`.
- **Changed or rejected:** I asked for the smallest fix that solves the defect, not a rewrite of the
  hook. The AI's first version also tripped a lint rule; it was changed to follow the project's
  existing `useAsyncData` pattern.
- **Mistakes caught:** the AI first said APPROACH.md wasn't required, because it had only searched
  `ASSIGNMENT_CANDIDATE.md`; the original README asks for it. In Task 2 it first wrote a history
  test that escalated a High ticket twice: the rule correctly refused the second step (already
  Critical), so the test was wrong, not the code. It also first bound the SLA config in the
  Infrastructure layer, which lacks the binder package; moved to `Program.cs` rather than add one.
- **Task 3:** the AI's first unknown-email path verified the password against a made-up hash
  string, which Identity's hasher would have rejected with an exception. It was changed to hash the
  password instead, which costs the same time.
- **Parallel work:** while the Docker image built, a separate Claude agent wrote the 2.5 report in
  its own git worktree, so it could not disturb the build. I reviewed its diff, re-ran its tests,
  ran it against SQL Server (it had only been tested on SQLite) and checked its totals against the
  SLA filter before copying it in.
- **A decision I made:** how AtRisk knows the window length (see Design decisions). The AI laid out
  three options; I chose deriving it from priority and tier.
- **How generated code was verified:** tests run on the old code (failing) and on the change (passing),
  typecheck and lint clean, the browser Network tab, and EF Core's SQL log.

## Known limitations

- In development, React StrictMode runs effects twice, so the first page load sends two identical
  requests. Production builds do not.
- If two ticket-list requests are in flight, a slower older response can land after a newer one and
  briefly show stale results. Pre-existing; would be fixed by ignoring or aborting outdated requests.
- Two tickets created at the same moment could both be given an agent's last free slot, putting them
  one over their limit: the open counts are read, then the ticket is saved. Fixing it needs a lock or
  a serialisable transaction around assignment.
- Changing the `Sla` config moves existing tickets' at-risk point, because AtRisk is derived from
  the current config (see Design decisions).
- Swagger has no "Authorize" button, so calls from the Swagger page get 401. Use the web app, or
  curl with `-H "Authorization: Bearer <token>"` (the token comes from `POST /api/auth/login`).
- No refresh tokens, lockout or rate limiting on login (see Authentication for production).
- The API build warns that `System.Security.Cryptography.Pkcs` 9.0.11 has known vulnerabilities.
  Pre-existing dependency; out of scope here.
- Out of scope per the brief: registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign.

## Time spent
Begun Oct 28 - Ended Oct 29 
Several sessions 

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

In Development the API applies migrations and seeds demo data on startup. Sign in with any agent
from "Development login credentials" above. **Never commit real
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
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. All applied in the database. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket; triaged automatically, with a `triage` object of reasons |
| `POST /api/auth/login` | Sign in: `{ "email", "password" }` → token (the only endpoint open without a token) |
| `GET /api/auth/me` | The signed-in agent |
| `POST /api/tickets/{id}/escalate` | Escalate one level: `{ "reason" }`; who escalated comes from the token |
| `GET /api/tickets/{id}/escalations` | Escalation history, newest first |
| `GET /api/reports/sla-summary` | SLA counts per agent or priority: `groupBy=agent|priority`, optional `from`, `to` |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `401` not signed in or failed login, `404` not found, `409` conflict or business rule.

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
