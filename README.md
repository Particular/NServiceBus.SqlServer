# SQL Server Transport for NServiceBus

Install this to enable NServiceBus to facilitate messaging over SQL Server

## Installation

Before doing anything else, make sure you have SQL Server up and running in your environment. Also make sure it is accessible from all the machines in your setup.

1. Add NServiceBus.SqlServer to your project(s). The easiest way to do that is by installing the [NServiceBus.SqlServer nuget package](https://www.nuget.org/packages/NServiceBus.SqlServer).

2. In your app.config make sure to provides the necessary connection information needed to communicate to SQL server. A typical setup would be:

````xml
<connectionStrings>
  <add name="NServiceBus/Transport" connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"/>
</connectionStrings>
````

## Performance

Consider creating a RAM drive or using the temporaty drive when running in a cloud vm and hosting your databases on it to reduce the time required to run acceptance tests.

## How to run tests

Tests expect a Sql Server instance (default: `.\SqlExpress`) to be available, and by default use the following connection string: `Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True`. It is possible to configure a different connection string by setting it as the value of the `SqlServerTransportConnectionString` environment variable. The initial catalog, `nservicebus`, is hardcoded in some tests and cannot be changed.

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
