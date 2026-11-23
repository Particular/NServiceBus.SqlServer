# Engineering context

## About this repository

NServiceBus.SqlServer provides transport support for Microsoft SQL Server and PostgreSQL. It produces the NServiceBus SQL Server and PostgreSQL transport packages; see the root [README](../README.md) for setup and local build and test instructions.

- `src/` — transport implementation, tests, and supporting projects
- `doc/` — operational guidance, including PostgreSQL connection pooling
- `.github/workflows/` — CI pipelines

## Start here

- [SQL Server transport documentation](https://docs.particular.net/transports/sql/)
- [PostgreSQL transport documentation](https://docs.particular.net/transports/postgresql/)
- [README.md](../README.md) — setup, build, and local testing
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Architecture and design

- [SQL Server transport transaction modes](https://docs.particular.net/transports/sql/transactions) — which operations share the receive transaction in each mode

## Guidelines

- [SQL statements and parameters](guidelines/sql-statements.md) — applies to a change that adds or edits statement text or its bound parameters; size every string and binary parameter, and turn implicit transactions off in statements that open their own
- [Guideline structure](guidelines/README.md) — how guidelines are written and indexed

## Upstream contracts

- [NServiceBus: message dispatchers](https://github.com/Particular/NServiceBus/blob/master/docs/message-dispatchers.md) — what the transport must honor when dispatching messages, including batching and dispatch consistency
- [NServiceBus: message receivers](https://github.com/Particular/NServiceBus/blob/master/docs/message-receivers.md) — what the transport must honor when receiving messages, including concurrency and shutdown
- [NServiceBus: for downstream components](https://github.com/Particular/NServiceBus/blob/master/docs/README.md#for-downstream-components) — contracts Core documents for downstream components

## Decisions and rationale

- [Architecture and design decisions](decisions/)

### Decisions recorded in pull requests

- Record the transaction mode when the transport creates the transaction, rather than inferring it during dispatch — [#1756](https://github.com/Particular/NServiceBus.SqlServer/pull/1756)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.
