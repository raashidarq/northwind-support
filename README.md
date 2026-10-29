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
| 2. Ticket escalation & assignment | Done (2.5 stretch report not attempted) |
| 3. Login and authentication | Not started |
| 4. Dockerise the API | Not started |

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
- No other tables changed. Escalation rows are never updated: the entity has private setters and no
  methods that change it.

## Testing

- Run everything: `npm test` (backend xUnit, then web Vitest). No database needed.
- Baseline before any change: 30 backend + 2 web tests, all passing. Now 101 backend + 18 web.
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
- Each endpoint was also called against SQL Server (200 / 400 / 404 / 409, history, triage).
- *Not tested:* search case-insensitivity. SQLite compares case-sensitively and SQL Server, by
  default, does not, so a test here would only be testing SQLite. Two tickets created at the same
  moment racing for the last slot of an agent's limit (see Known limitations).

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
- The `Sla` section is only in `appsettings.Development.json`; the API refuses to start without it.
- `escalatedBy` is free text until login (Task 3) can supply the agent.
- The API build warns that `System.Security.Cryptography.Pkcs` 9.0.11 has known vulnerabilities.
  Pre-existing dependency; out of scope here.
- Out of scope per the brief: registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign.

## Time spent

TODO

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
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. All applied in the database. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket; triaged automatically, with a `triage` object of reasons |
| `POST /api/tickets/{id}/escalate` | Escalate one level: `{ "reason", "escalatedBy" }` |
| `GET /api/tickets/{id}/escalations` | Escalation history, newest first |
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
