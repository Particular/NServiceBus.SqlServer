# SQL Server Transport for NServiceBus

Install this to enable NServiceBus to facilitate messaging over SQL Server

## Installation

Before doing anything else, make sure you have SQL Server up and running in your environment. Also make sure it is accessible from all the machines in your setup.

1. Add NServiceBus.SqlServer to your project(s). The easiest way to do that is by installing the [NServiceBus.SqlServer nuget package](https://www.nuget.org/packages/NServiceBus.SqlServer).

2. In your app.config make sure to provides the necessary connection information needed to communicate to the RabbitMQ server. A typical setup would be:

````xml
<connectionStrings>
  <add name="NServiceBus/Transport" connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"/>
</connectionStrings>
````

## Samples

See https://github.com/Particular/NServiceBus.SqlServer.Samples
