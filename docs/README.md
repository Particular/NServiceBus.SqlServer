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

- [Architecture and design decisions](decisions/) — durable design constraints and rationale

### Decisions recorded in pull requests

- Record the transaction mode when the transport creates the transaction, rather than inferring it during dispatch — [#1756](https://github.com/Particular/NServiceBus.SqlServer/pull/1756)
