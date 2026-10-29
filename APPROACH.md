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
