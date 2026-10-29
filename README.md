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
| 2. Ticket escalation & assignment | Not started |
| 3. Login and authentication | Not started |
| 4. Dockerise the API | Not started |

## What was implemented and the rules as understood

- **DEFECT-117:** every filter change on the ticket list now sends a request straight away; only the
  search box waits for typing to stop (300 ms).
- **Server-side filtering:** every list filter (search, status, priority, category, customer,
  assigned agent, unassigned only) is applied in the database, and the total count matches the
  filtered list.

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
`TicketQueriesTests`. Task 2's `slaStatus` is the exception: the SLA status is computed in C#
(`SlaEvaluator`), which SQL can't run, so it has to be written as date conditions on `DueAtUtc` /
`ResolvedAtUtc`.

## Testing

- Run everything: `npm test` (backend xUnit, then web Vitest). No database needed.
- Baseline before any change: 30 backend + 2 web tests, all passing. Now 36 backend + 10 web.
- **DEFECT-117** (`apps/web/src/features/tickets/hooks/useTicketList.test.ts`): 8 tests on the hook
  with the API mocked and timers faked, asserting which request is sent and when.
- **Filtering** (`apps/api/tests/SupportDesk.UnitTests/Infrastructure/Queries/TicketQueriesTests.cs`):
  6 tests that run the real query against an in-memory SQLite database (not a C# list), so they prove
  the filters translate to SQL. They cover priority, several filters combined, customer-name search
  with surrounding spaces, blank search, unassigned only, and the total count when the page is smaller
  than the result. 5 of 6 fail on the old code (blank search passes, because ignoring it is what the
  old code did anyway).
- *Not tested:* search case-insensitivity. SQLite compares case-sensitively and SQL Server, by
  default, does not, so a test here would only be testing SQLite.

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
- **Mistake caught:** the AI first said APPROACH.md wasn't required, because it had only searched
  `ASSIGNMENT_CANDIDATE.md`; the original README asks for it.
- **How generated code was verified:** tests run on the old code (failing) and on the change (passing),
  typecheck and lint clean, the browser Network tab, and EF Core's SQL log.

## Known limitations

- In development, React StrictMode runs effects twice, so the first page load sends two identical
  requests. Production builds do not.
- If two ticket-list requests are in flight, a slower older response can land after a newer one and
  briefly show stale results. Pre-existing; would be fixed by ignoring or aborting outdated requests.
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
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. All applied in the database. |
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
