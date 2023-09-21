# SQL Server Transport for NServiceBus

Install this to enable NServiceBus to facilitate messaging over SQL Server

## Installation

Before doing anything else, make sure you have SQL Server up and running in your environment. Also make sure it is accessible from all the machines in your setup.

1. Choose which package you want to use:
   - [NServiceBus.Transport.SqlServer](https://www.nuget.org/packages/NServiceBus.Transport.SqlServer) — references [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
   - [NServiceBus.SqlServer](https://www.nuget.org/packages/NServiceBus.SqlServer) — references [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient)
2. Add the package to your project(s).
2. In your app.config make sure to provides the necessary connection information needed to communicate to SQL server. A typical setup would be:
   ```xml
   <connectionStrings>
     <add name="NServiceBus/Transport" connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true"/>
   </connectionStrings>
   ```

## Performance

Consider creating a RAM drive or using the temporaty drive when running in a cloud vm and hosting your databases on it to reduce the time required to run acceptance tests.

## How to run tests

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

## Samples

See http://docs.particular.net/samples/
