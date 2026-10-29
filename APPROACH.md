# Approach

How I worked through each task and why. The decisions themselves are summarised in the
[README](README.md#design-decisions); this file is the thought process behind them.

## Task 1: Defect fix and server-side filtering

### DEFECT-117

1. **Reproduce before reading code.** In the browser's Network tab, changing Status to *In progress*
   sent no `GET /api/tickets`. Typing a letter in Search then sent a request with `status=InProgress`,
   so the new status *was* in state but no request was being made for it.
2. **Find the cause.** That pointed at whatever decides *when* a request happens: the `useEffect` in
   `useTicketList`. Its dependency list named only `search`, `page` and `pageSize`, and the linter's
   missing-dependency warning had been switched off with a comment saying the `filters` object
   "changes identity every render". It doesn't: React keeps the same state object until it is set.
   So every other filter changed state without re-running the effect.
3. **Test first.** I wrote tests that change each filter and assert that the (mocked) API is called
   at once, with the new value. I ran them against the original code to see them fail (7 of them),
   so I knew they actually detect the bug.
4. **Fix the cause, minimally.** The effect was doing two jobs: waiting for typing to stop, and
   fetching. Adding the missing fields to its list would have made dropdowns wait 300 ms for a
   debounce meant for typing. So I split it: the search box's text has its own state that a small
   effect copies into `filters` after 300 ms, and the fetch effect depends on the whole `filters`
   object (following the existing `useAsyncData` pattern). I kept the rest of the hook as it was.
5. **Guard against the obvious wrong fix.** A test that types three letters quickly and expects one
   request makes sure the fix didn't simply remove the debounce.
6. **Check in the browser again**, then commit the fix and the tests separately.

### Server-side filtering

1. **Find where the list is built.** `TicketQueries.GetPagedAsync` already sorted and paged in the
   database, but never looked at the filter fields, and counted before anything else.
2. **Keep it in the database.** The query is an `IQueryable`, so adding `Where` calls only describes
   the query; EF Core turns them into one SQL `WHERE` when it is run. I added one `Where` per filter,
   in a private `ApplyFilters` method written like the existing `ApplySort`, and applied it *before*
   the count so the total matches the filtered list.
3. **Test against a real database.** Testing on a C# list would pass even if a filter could not be
   translated to SQL, so the tests use the in-memory SQLite database the test project was already set
   up for. 5 of the 6 failed before the change.
4. **Check the SQL.** EF Core logs each query in the API terminal in development; after picking a
   priority, both the count and the page query carried a `WHERE` on `Priority`.
5. **Looked ahead.** Task 2's SLA filter can't be added the same way, because the SLA status is
   calculated in C#. I noted that it will need to be expressed as date conditions instead.

## Task 2: Ticket escalation & assignment

The brief says the rules are simple and the challenge is where they go, so I decided that first.

1. **Read what was there.** `SlaStatus` already had an `AtRisk` value that `SlaEvaluator` never
   returned; categories already carried `ForcesCriticalPriority` and `RequiresSpecialist`; an `Sla`
   config section existed but nothing read it; the architecture tests forbid the domain from
   depending on anything. That pointed to: rules as plain domain classes, numbers from config.
2. **Rules first, alone (2A).** `PriorityRule`, `SlaPolicy`, `AgentSelector` and AtRisk in
   `SlaEvaluator`, each with unit tests and no database. One real decision came up: AtRisk needs the
   window's length, and after an escalation `Due - Created` is wrong. I chose to derive it from the
   current priority and tier (see README), which needs no new column.
3. **Triage on create (2B).** The handler only gathers inputs (customer tier, category flags, agents
   with open counts from one query) and calls the rules. I checked it against the real database:
   a Low Outage ticket for a Premium customer became Critical, due in 2 h, assigned to the Outage
   specialist with the fewest open tickets.
4. **Escalation (2C).** Put on the `Ticket` aggregate so it can't be bypassed, like the existing
   `ChangeStatus`. History is part of the ticket and can't be changed. While writing
   "keep the agent if still eligible" I noticed an agent at their limit would be "ineligible" for a
   ticket they already hold, so the check leaves that ticket out of their count.
   Every response (200 / 400 / 404 / 409) was called against SQL Server.
5. **SLA filter (2D).** The evaluator can't run in SQL, so I rewrote each status as date
   comparisons, with the 8 at-risk cut-offs computed up front and chosen per row by a `CASE`. The
   tests check every result against the evaluator itself, and I broke a boundary on purpose to see
   them fail.
6. **Frontend (2E).** Reused the existing `SlaCell` (it was already on both pages) and made its labels
   cover every state. The SLA filter needed no change to the list hook, because the DEFECT-117 fix
   already sends a request for any filter field. The escalation form checks the same limits as the
   server before sending, and handles a 409 anyway.
