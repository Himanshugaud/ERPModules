# ERP Performance Verification

Date: 2026-10-18. Scope: build measurements, deterministic cache concurrency workload, and static source signals. No representative ERP HTTP load benchmark was possible.

Follow-up: 2026-10-24. Frontend typecheck/build pass again; build duration 8.22 s and artifact sizes match the post-update values below. New request-time session validation performs up to four sequential database reads per authenticated request (organization, user, role names, permission codes) without caching authorization state. This is code-path analysis, not measured latency or throughput. Profile it under representative isolated workloads; any optimization must retain revocation correctness. No runtime load test was performed.

Later authorized setup on 2026-10-24 created erpmodulessqldb-staging on the existing Azure SQL server: Online, Basic 5 DTUs, 2 GB. All six schema scripts are applied (69 tables); both local SQL configurations use its restricted contained identity. A subsequent explicit request copied 9 production users and 642 items plus reference dependencies without password hashes/external identities/transactions, and selected private container erp-attachments-local for the local API. Blob upload/download/cleanup passed; API reads returned 9 users and 634 non-deleted items, with anonymous access rejected. Four explicit Azure SQL tests passed again with synthetic cleanup. These are bounded functional checks, not a runtime load benchmark or normal-login/full-workflow proof. Basic-tier results must not be presented as production-like capacity measurements.

## Environment and Safety

- Windows workstation, Node v22.16.0, .NET SDK 9.0.319, net9.0 test target.
- React 18 / Vite 5 production builds; xUnit 2.9.2 through VSTest.
- Original audit/performance measurements exercised no production records/endpoints. The later explicit reference-copy request authorized read-only production data access; all test writes remained staging/local-container only.
- The original .NET SQL integration setup targeted only a generated LocalDB database; that engine still fails before application behavior can be tested. The later four Azure staging SQL tests are a separate explicit harness, not proof that the retained .NET tests pass.
- Local SQL and attachment object targets are separate from production, but share the SQL server/storage account and retain existing account-wide access. Local passwords and the later authorized client/warehouse/opening-stock seed are configured; representative synthetic workload scenarios, load limits and performance instrumentation still need setup. The opening import preserved 11 balances through 9 ADJUSTMENT lines in one atomic transaction (2 warehouse-specific documents); signed-token API checks and four SQL tests pass, not a load benchmark. Copied production-derived records need restricted access. k6 was not installed; no general-purpose smoke/seed script was used for load testing.

## Measured Cache Workload

Test: ReferenceCacheTests.ConcurrentMissesShareOneLoader. One synthetic organization, group/key pair, initially empty cache, 16 overlapping callers, one asynchronous gate keeping all loader attempts in flight. No SQL, network, or real records. The baseline regression failed with Actual=16; after the fix it passes with one loader call.

| Measurement | Before | After |
| --- | --- | --- |
| Concurrent cache callers | 16 | 16 |
| Loader invocations for one key | 16 | 1 |
| Returned values | Same synthetic value | Same synthetic value |
| Cross-organization cache separation | Passing existing/new test | Passing |
| Retry after load failure | Passing | Passing |
| Invalidation while load is in flight | Passing | Passing |

This establishes reduced redundant work for one process/key, not 16x throughput, reduced SQL latency, or distributed-cache coherence. MemoryCache is configured for 2048 value entries with TTL; this is not a byte-based memory limit. Version metadata and in-flight loads require separate cardinality consideration. No GC/allocation/CPU benchmark was run.

## Frontend Build Measurements

Captured using `npm --prefix frontend run build`. Single workstation runs, not statistically controlled latency benchmarks. Sizes are Vite-reported decimal kB. ExcelJS remains a separate lazy-loaded chunk.

| Artifact | Baseline | Post-update |
| --- | --- | --- |
| Main JavaScript | 419.52 kB | 419.57 kB |
| Main JavaScript gzip | 108.74 kB | 108.79 kB |
| ExcelJS chunk | 938.56 kB | 938.56 kB |
| ExcelJS gzip | 271.04 kB | 271.04 kB |
| CSS | 18.56 kB | 18.56 kB |
| Build duration, captured runs | 8.41 s | 8.21 s final run (other patched runs: 7.83 s, 8.85 s) |

No performance improvement is inferred from variable build duration. The large ExcelJS chunk warning remains. Frontend render time, network transfer, interaction latency, React re-render behavior, and memory leaks were not profiled.

## Required Runtime Metrics

| Metric | Result/reason unavailable |
| --- | --- |
| HTTP throughput, requests/second | Not measured; no isolated running ERP/database |
| Response p50, p95, p99 | Not measured |
| HTTP error rate | Not measured; unit-test pass counts are not request error rate |
| CPU/memory/GC | Not profiled |
| Database query p50/p95/p99 and plans | Not measured; LocalDB startup blocked |
| Connection-pool usage/timeouts | Not measured |
| Lock waits/deadlocks/transaction retries | Not measured; retained SQL tests blocked |
| Sustained/spike concurrency | Not measured against ERP; only the 16-caller synthetic cache test ran |

## Static Scan Checklist

Initial 2026-10-18 lexical snapshot of 105 tracked production C# files; generated bin/obj output and test files excluded. Counts are historical signals, not confirmed defects or refreshed totals after the 2026-10-24 session-validation changes. No blanket LINQ/string/serialization rewrite was made.

| Recipe | Hits |
| --- | --- |
| Sync `.Result`, `.Wait()`, GetAwaiter().GetResult() | 0 / 0 / 0 |
| ValueTask, stackalloc, `new HttpClient()` | 0 / 0 / 0 |
| Substring, IndexOf literal without comparison | 0 / 0 |
| StartsWith/EndsWith literal, Contains literal | 0 / 0 |
| Culture-sensitive ToLower/ToUpper | 0 |
| Replace, params declarations | 1 / 3 |
| Select/Where/OrderBy/GroupBy lexical calls | 242 |
| Any/All lexical calls | 8 |
| New Dictionary/List expressions | 15 |
| Static readonly Dictionary | 0 |
| Compiled regex, new Regex, GeneratedRegex | 0 / 0 / 0 |
| Explicit `new JsonSerializerOptions` expression | 0 (target-typed cached options exist) |
| FromSqlRaw/ExecuteSqlRaw | 0 |
| Public/internal unsealed class declarations | 51 |
| Sealed class declarations | 298 |
| IEquatable declaration signal | 0 |
| IndexOfAny, ToCharArray, string.Format | 0 / 0 / 0 |
| Explicit new byte array allocation expression | 0 |
| JsonSerializer.Serialize calls | 4 |
| HttpClient type references | 0 |

The declaration inverse check is 298 sealed versus 51 public/internal unsealed matches, 349 total lexical declaration matches. Most unsealed classes are domain entities, not a demonstrated hot-path problem. Target-typed allocations, multiline variations, aliases, and semantic relationships are outside these simple recipes.

Positive review observations: async EF calls with cancellation in the reviewed repositories; AsNoTracking read paths; server-side PageSize capped at 100; allowlisted sort switches; reused JSON options in HTTP/exception helpers; tenant-keyed reference values; SQL rowversion for stock balances. These do not prove every query is bounded or every tenant predicate is present.

### Follow-Up Candidates, Not Benchmarked Claims

- Inventory create/update loops issue per-line reference queries. Profile representative document sizes before replacing them with tenant-scoped bulk checks.
- Item lookup/catalog paths intentionally load organization-wide data. Measure organization size, payload, and memory; do not remove tenant filters while optimizing.
- The frontend session cache and server reference cache are process/session scoped. Verify invalidation and stale-response behavior under user/organization changes and multi-instance writes.
- Pagination arithmetic can overflow for extreme page numbers, and lists/reports may fetch incomplete capped pages. Add boundary/large-dataset tests before claiming correct large-data behavior.
- Query plans, FK/index coverage, wildcard search cost, and lock contention remain unmeasured. SQL source index declaration counts are not evidence of runtime plan quality.

| Classification | Confirmed Count | Main Result |
| --- | --- | --- |
| Critical measured performance defects | 0 | No ERP runtime profile available |
| Medium confirmed scalability defects fixed | 1 | Cache fan-out 16 loaders to 1; bounded value storage |
| Unverified optimization groups | 5 | Candidates above require profiling |

Static analysis can miss defects and suggest irrelevant optimizations. These results require human review and representative profiling before performance or capacity commitments.

## Next Isolated Workload Plan

Use an approved disposable SQL Server/Azure SQL database with the actual schema scripts, isolated blob storage, and synthetic organizations/users only. Do not repoint these tests to production. Start with two organizations, distinct roles, 10,000 representative items, 1,000 projects, and a documented initial stock/PO dataset. These are proposed dataset sizes, not data generated in this audit.

1. Run the retained SQL category first, then receipt/transfer/work-order service tests. Two concurrent users must yield one success/one conflict for the same stale state, with exactly one ledger effect and no partial outbox/audit persistence. Test first-stock-row creation and duplicate business numbers against actual constraints.
2. Establish a warmed single-user baseline. Then test 5, 20, and 50 concurrent users for read-heavy (90/10), write-heavy (30/70), and mixed (70/30) read/write request mixes. Isolate records per user except deliberate contention tests.
3. Include paginated projects/items/stock, document lookup/download, distinct-record edits, PO receipt approvals, and dispatch/receive. Repeated non-idempotent requests must not be blindly retried by the load driver.
4. Sustain each level for 10 minutes after warmup, then a bounded 2-minute spike at twice the planned concurrency. Abort on cross-tenant data, negative/unreconciled stock, unexpected writes, or sustained resource exhaustion. Agree on environment-specific SLO/error/CPU/connection thresholds before running.
5. Capture per-route throughput, p50/p95/p99, expected 409 versus unexpected errors, process CPU/RSS/GC, SQL query timings/plans, pool occupancy/timeouts, waits/deadlocks, and correlated logs. Publish raw results, dataset/reset procedure, commit, settings, and exact request mix alongside before/after comparisons.

This plan is not executed evidence. Release and capacity approval remain blocked until the isolated environment and measurements exist.