# NServiceBus.SqlServer 

NServiceBus.SqlServer provides support for sending messages using [Microsoft SQL Server](http://www.microsoft.com/sqlserver) or [PostgreSQL](https://www.postgresql.org.pl/) without the use of a service broker.

It is part of the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems.

See the [SQL Server transport documentation](https://docs.particular.net/transports/sql/) and [PostgreSQL transport documentation](https://docs.particular.net/transports/postgresql/) for more details on how to use it.

## Installation

### SQL Server 

Before doing anything else, make sure you have SQL Server up and running in your environment. Also, make sure it is accessible from all the machines in your setup.

1. Choose which package you want to use:
   - [NServiceBus.Transport.SqlServer](https://www.nuget.org/packages/NServiceBus.Transport.SqlServer) — references [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
   - [NServiceBus.SqlServer](https://www.nuget.org/packages/NServiceBus.SqlServer) — references [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient)
1. Add the package to your project(s).
1. In your code provide the necessary connection information to communicate with the SQL server instance. A typical setup would be:
   ```csharp
   <connectionStrings>
     <add name="NServiceBus/Transport" connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true"/>
   </connectionStrings>
   ```

### PostgreSQL

1. Add [NServiceBus.Transport.PostgreSql](https://www.nuget.org/packages/NServiceBus.Transport.PostgreSql) the package to your project(s).
1. In code [provide connection string information](https://docs.particular.net/transports/postgresql/connection-settings#connection-configuration) necessary to connect to the PostgreSQL database, e.g.:
   ```csharp
   var transport = new PostgreSqlTransport("User ID=<user>;Password=<pwd>;Host=localhost;Port=5432;Database=nservicebus;Pooling=true;Connection Lifetime=0;");
   ```
   
### Connection pooling

Deployments with multiple endpoints running on PostgreSQL require external connection pooling e.g. [using pgBouncer](/docs/postgre-with-pgbouncer.md)

## Testing 

### Performance

Consider creating a RAM drive or using a temporary one when running in a cloud VM and hosting your databases to reduce the time required to run acceptance tests.

### Running tests locally

The tests expect a SQL Server instance to be available.

All tests use the default connection string `Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true`. This can be changed by setting the `SqlServerTransportConnectionString` environment variable. The initial catalog, `nservicebus`, is hardcoded in some tests and cannot be changed.

### Requirements

- MSDTC is required to run tests.
- The following databases must be created in advance in the configured instance:
  - `nservicebus`
  - `nservicebus1`
  - `nservicebus2`
- The following schemas must be created in advance in the `nservicebus` database
  - `receiver` owner `db_owner`
  - `sender` owner `db_owner`
  - `db@` owner `db_owner`
