# Bucketed queue table layout for SQL Server

The SQL Server transport can create queue tables clustered on a computed bucket, so that a drained queue releases its pages. Users opt in with `SqlServerTransport.CreateBucketedQueueTables`.

## Context

SQL Server transport queue tables are heaps with a nonclustered index on `RowVersion`, named `Index_RowVersion`. A heap frees a page that single-row deletes empty only under a table lock or a bulk delete. The transport receives one row at a time with `ROWLOCK`. A queue table therefore keeps every page it ever needed. Its size grows with the concurrency it has handled, not with the number of messages it holds.

In a local reproduction on SQL Server 2022, 8 connections inserted 20,000 rows and 8 connections drained them with the shipped receive statement. At zero rows, the heap held 2,869 data pages and the bucketed layout held 4.

Customers rebuild these tables on a schedule, accept downtime, or run production on untested schema changes. Internal context was consulted.

A queue table layout has two further constraints:

- Endpoint instances on different transport versions share queue tables during a rolling upgrade. The statements of every supported version must work on every layout.
- Writers outside the transport, such as native sends, stored procedures, and scripts, also write to queue tables. A layout must not add session requirements for them.

### Why queue tables are heaps

Queue tables had a clustered index on `RowVersion` until [#613](https://github.com/Particular/NServiceBus.SqlServer/pull/613) removed it. [#624](https://github.com/Particular/NServiceBus.SqlServer/issues/624) reported page latch contention on that index that limited throughput above 8 threads. There are two explanations:

- #624 states that the first data page of the clustered index contains a pointer to the first non-empty data page. It states that this pointer changes after each delete from a queue, which causes contention.
- The planning of the bucketed layout attributed the slowness to contention between inserts at the last page of the index.

### Validation of both explanations

Both explanations hold, for different parts of the workload. The pointer that #624 describes changes when a page empties, not after each delete.

The tests ran on 2026-11-25 on SQL Server 2022 (16.0.4325.4, Developer edition) in a Linux container on a laptop with 8 logical processors. They compared the 6.0.0 table with the clustered index, the heap, and the bucketed layout. Each session ran the current send or receive statement in a server-side loop for 10 seconds, with one statement per transaction and 1 KB bodies. A mixed run had the stated number of senders and the same number of receivers. Receive-only runs started with 150,000 rows, or 400,000 with delayed durability. Mixed runs started with 2,000.

The table shows rows per second, as the median of 3 runs with full durability. Single runs varied: one heap run with 8 insert sessions reached only 842.

| Workload | Sessions | Clustered on `RowVersion` | Heap | Bucketed |
|---|---:|---:|---:|---:|
| Insert only | 8 | 2,991 | 2,740 | 2,349 |
| Insert only | 32 | 4,616 | 6,333 | 6,267 |
| Insert only | 64 | 4,300 | 6,051 | 6,242 |
| Receive only | 8 | 941 | 1,262 | 1,428 |
| Receive only | 32 | 723 | 1,714 | 1,710 |
| Receive only | 64 | 685 | 1,799 | 1,948 |
| Mixed, inserted | 8 | 1,315 | 1,410 | 1,381 |
| Mixed, inserted | 32 | 1,841 | 2,335 | 2,041 |
| Mixed, inserted | 64 | 1,736 | 1,924 | 1,552 |
| Mixed, received | 8 | 804 | 1,188 | 1,049 |
| Mixed, received | 32 | 632 | 1,433 | 1,166 |
| Mixed, received | 64 | 679 | 1,492 | 1,231 |

Insert contention:

- At 32 and 64 sessions, the clustered table inserted 27% to 29% fewer rows than the heap.
- Two further runs per session count located the page latch waits of the clustered table. The end of the index took 97% to 98% of the samples. The last page of index level 1 took 72% to 78%, and the last leaf page 20% to 25%.

Receive contention:

- A separate test received 700 rows one at a time from the clustered table. The first leaf page stayed first after its rows were deleted.
- That page kept one ghost row, which was still there 60 seconds after the table was empty.
- After 70, 210, and 420 receives, the next-page pointer of that page led to the page of the oldest live row. The pointer changes only when that page empties, about every 7 receives with 1 KB bodies.
- In receive-only runs, the clustered table trailed the heap by 25% at 8 sessions and by 58% to 62% at 32 and 64. Its rate fell as sessions increased.
- At 32 and 64 sessions, the first leaf page took 46% to 50% of its sampled waits. Later leaf pages with ghost rows took 34%.
- `Index_RowVersion` on the heap keeps its first leaf page in the same way. That page holds about 368 entries, so its pointer changes much less often.

Delayed durability removes log flushes as a limit. With it, the clustered table inserted 34% fewer rows than the heap in insert-only runs at 32 and 64 sessions. It received 53% to 61% fewer in receive-only runs.

## Decision

The transport offers a bucketed queue table layout and keeps the heap as the default.

- The bucketed layout adds the computed column `Bucket`, defined as `ISNULL(CONVERT(smallint, RowVersion % 64), 0)`, and a unique clustered index on `(Bucket, RowVersion)`. `Index_RowVersion` and `Index_Expires` are the same as on the heap.
- `ISNULL` makes the column non-nullable, which the unique clustered key needs. `smallint` keeps the key at 10 bytes. The column is not persisted, and the bucket count is fixed at 64.
- `CreateBucketedQueueTables` defaults to `false`. When it is `true`, installers create each missing queue table with the bucketed layout. The transport never alters, migrates, or recreates an existing queue table.
- No statement that reads or writes queue rows depends on the layout. The [SQL statements guideline](../guidelines/sql-statements.md) states this rule.
- When the setting is `true` and an input queue is a heap, the transport logs one Info message at startup. The message recommends no migration, because no migration guide exists.
- The next major version makes the bucketed layout the only one that the transport creates, and removes the setting.

Consecutive inserts go to different buckets, which spreads them over 64 places in the clustered index. Rows within a bucket keep `RowVersion` order. Receives find rows through `Index_RowVersion`, which orders all buckets together, and then look up each row in the clustered index.

## Consequences

A drained bucketed table releases its pages after SQL Server removes the ghost rows. In the validation runs, the bucketed layout inserted about as many rows as the heap at 32 and 64 sessions. In receive-only runs, at most 3% of its sampled page latch waits were on its clustered index.

The layout has these costs and limits:

- Throughput under mixed load was lower than on the heap with full durability. At 8 to 64 sessions, it received 12% to 19% fewer rows and inserted 2% to 19% fewer. With delayed durability, it received 17% fewer at 32 sessions and 5% more at 64. It inserted 8% fewer and 13% more. The heap stays the default. Plan, throughput, and soak tests on the target platforms must run before release.
- A receive does a key lookup into the clustered index, where the heap does a row ID lookup. The layout ships only if a receive reads no more pages than on the heap. The validation runs did not measure page reads.
- Every insert still passes through the last page of `Index_RowVersion`. A planning measurement with 32 senders counted 112,000 of 118,000 page latch waits on it. The validation runs found 95% of sampled waits there with 32 senders. This limit is accepted, because the layout spreads data pages and leaves that index unchanged.
- Rows deleted while an older row-versioned transaction is open stay allocated on both layouts. Only the bucketed table releases them after that transaction ends. The limit is accepted, and the product documentation must state it.
- `INSERT INTO ... SELECT *` between queue tables of different layouts fails, because `SELECT *` returns `Bucket`. Tables with the `BodyString` column already behave this way. The product documentation must tell script authors to name columns.
- `Bucket` is computed for each row that `SELECT TOP(1) *` reads. A plan comparison with a persisted column must run before release. The creation script changes to `PERSISTED` if that is measurably cheaper.
- Existing queues gain nothing unless users recreate them, and no migration script ships. The instance that creates a table first decides its layout. This is accepted, because every transport version works with both layouts.
- Writers with session options that indexes on computed or filtered columns reject get error 1934. `Index_Expires` already imposes this requirement, and an integration test shows that the bucketed layout adds none.

Users roll back by setting the property to `false`. Queues created afterwards are heaps, and existing bucketed tables keep working.

Follow-up work is in other repositories. docs.particular.net must document the setting, the bucketed create-queue script, and the limits above. ServiceControl gets the layout for its queues only if it exposes the setting.

## Alternative approaches

The first two alternatives are the points of leverage, because each releases pages without a new column.

- Cluster on `RowVersion` alone: a planning measurement with 64 senders inserted 8,697 rows per second, against 13,709 for the heap. The planning record does not state the environment. The validation runs above show both kinds of contention. No measurement tested `OPTIMIZE_FOR_SEQUENTIAL_KEY`, which targets last-page insert contention. Rejected.
- Keep the heap, and rebuild it or delete with `TABLOCK`: both need a table lock that blocks receives. Rejected.
- Cluster on `Id` or another random key: this avoids a hot page, but causes page splits and loses the order within pages. It was not measured. Rejected because of fragmentation.
- Make the bucket count configurable: with both 32 and 64 buckets, the receive side limited throughput. A fixed count keeps one creation script and one validated layout. Rejected.
- Use one creation script with placeholders for the extra column and index: this avoids duplicated text. It formats SQL fragments into SQL and makes the heap script harder to compare with earlier releases. An integration test guards against divergence instead. Rejected.
- Select the layout with an AppContext switch: this adds no public API to remove later, but users find it only in documentation. Rejected.
- Add an options type with a queue table layout enum: this leaves room for layouts that nobody has proposed, and adds more API to remove in the next major version. Rejected.
