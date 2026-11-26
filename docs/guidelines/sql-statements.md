# SQL statements and parameters

## Applies when

A change adds or edits the text of a statement that a transport sends to the database, or the parameters bound to that statement. The rules cover SQL Server and PostgreSQL unless a rule names one.

## Rules

### Parameter sizes

1. Set `Size` explicitly on every string and binary parameter. Without it, SqlClient binds `nvarchar(n)` or `varbinary(n)` from each value, and every distinct length gets its own cached plan.
2. Size a parameter for a `max` column with `-1`.
3. Size a parameter that is compared with a bounded column with a fixed size, `4000` for `nvarchar`. Do not use `-1` there: SQL Server adds a `Filter` operator, stops pushing the predicate into the scan, and uses four to five times the CPU.
4. Never size a parameter below the longest value it can carry unless truncation demonstrably preserves the operation's semantics. SqlClient and Npgsql truncate the value to `Size`. For comparisons, verify both providers' matching behavior, including SQL Server's trailing-space padding; a size above the column length alone does not prove safety.
5. Expect one plan per item count for an `IN` list. Sizing does not change that.
6. Keep the parameter code shared between the two transports. `Size` does not change plans on PostgreSQL, so a provider-specific branch adds code without a benefit.
7. Verify a sizing change on SQL Server by comparing the number of cached plans, the plan shape, and the CPU per execution. The estimated cost and the logical reads are identical for the sizes in rule 3, so they do not show the difference.
8. Cover the sizes with a unit test on the generated command, and check that the test fails without the change.

### Transactions and session state on SQL Server

9. Start a statement that opens its own transaction with `SET IMPLICIT_TRANSACTIONS OFF`. A connection can run with the setting on. `BEGIN TRANSACTION` then nests inside the implicit transaction, `COMMIT` leaves `@@TRANCOUNT` at 1, and a lock owned by the transaction stays held on the pooled connection. Parameterized statements and plain batches behave the same.
10. Add `SET XACT_ABORT ON` to a statement that holds a lock for the duration of its transaction, so a cancellation or an error rolls the transaction back instead of leaving it open on the pooled connection.
11. Test statements that open and complete their own transaction on a connection with `IMPLICIT_TRANSACTIONS ON`. Assert that `@@TRANCOUNT` is zero and that no application lock is held afterwards. For statements participating in a caller-owned transaction, verify that they preserve transaction ownership and that no transaction or application lock remains after the caller commits or rolls back.

### Queue table layouts on SQL Server

12. Name every column that a statement inserts into a queue table or returns from it, and reference neither `Bucket` nor the clustered index. The statement then runs on both queue table layouts, and every supported transport version can share the table. `SELECT *` is allowed only where its columns reach no result set and no table, as in the common table expression of the receive statement. See the [bucketed queue table layout decision](../decisions/2026-11-25-bucketed-queue-table-layout.md).

## Evidence

- Rules 1 to 8: [#1253](https://github.com/Particular/NServiceBus.SqlServer/issues/1253), [#1728](https://github.com/Particular/NServiceBus.SqlServer/pull/1728), and [#1839](https://github.com/Particular/NServiceBus.SqlServer/pull/1839). Measured on SQL Server 2022 and 2025 with 200,000 subscription rows.
  - A delayed message body without a size produced 8 cached plans for 8 body lengths, and 1 plan with `-1`.
  - A topic list without a size produced 4 plans for 4 length combinations. Sizes of `200` and `4000` produced 1 plan with a clustered index scan and the predicate in the scan, at about 24 ms of CPU. A size of `-1` produced 1 plan with a `Filter` operator, at about 105 to 122 ms.
  - On PostgreSQL 17 with Npgsql, `pg_stat_statements` showed one entry per statement shape for every size, with the same cost.
  - Both providers truncated a 250 character value to 200 characters with `Size = 200`.
- Rules 9 to 11: [#1840](https://github.com/Particular/NServiceBus.SqlServer/pull/1840). The behavior of rule 9 was reproduced on SQL Server 2022 and 2025 with a statement that takes an application lock inside an explicit transaction. Rule 10 follows the code comment and the integration test in that pull request, which were not re-measured.
- Rule 12: the [bucketed queue table layout decision](../decisions/2026-11-25-bucketed-queue-table-layout.md). These integration tests passed on SQL Server 2022:
  - [`When_creating_queue_tables`](../../src/NServiceBus.Transport.SqlServer.IntegrationTests/When_creating_queue_tables.cs): both layouts have the same columns, apart from `Bucket`, and the same nonclustered indexes.
  - [`When_checking_schema`](../../src/NServiceBus.Transport.SqlServer.IntegrationTests/When_checking_schema.cs): the startup index checks find both indexes on a bucketed table.
  - [`When_moving_due_delayed_messages`](../../src/NServiceBus.Transport.SqlServer.IntegrationTests/When_moving_due_delayed_messages.cs): both delayed message movers insert into a bucketed table.
  - [`When_writing_with_non_default_session_options`](../../src/NServiceBus.Transport.SqlServer.IntegrationTests/When_writing_with_non_default_session_options.cs): writes under non-default session options have the same outcomes on both layouts.

  The [CI workflow](../../.github/workflows/ci.yml) also runs the acceptance and transport tests against bucketed tables. The statements that read and write queue rows match release 9.1.1, except the delayed message mover, which inserts into the queue with the same column list.
