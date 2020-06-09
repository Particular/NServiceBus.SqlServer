# Testing

To be able to run the tests:

- Create an environment variable named `SqlServerTransportConnectionString` with the following connectionstring:
  `Server=.\sqlexpress;database=nservicebus;Integrated Security=true;`
- Execute the following T-SQL on your `.\sqlexpress` instance.

```
USE [master]
GO

IF NOT DB_ID('nservicebus') IS NOT NULL
  CREATE DATABASE [nservicebus]
GO

IF NOT DB_ID('nservicebus1') IS NOT NULL
  CREATE DATABASE [nservicebus1]
GO

IF NOT DB_ID('nservicebus2') IS NOT NULL
  CREATE DATABASE [nservicebus2]
GO

USE [nservicebus]
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'receiver')
  EXEC('CREATE SCHEMA [receiver]')

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sender')
  EXEC('CREATE SCHEMA [sender]')

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'source')
  EXEC('CREATE SCHEMA [source]')

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'src')
  EXEC('CREATE SCHEMA [src]')

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'db@')
  EXEC('CREATE SCHEMA [db@]')

USE [nservicebus1]
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'src')
  EXEC('CREATE SCHEMA [src]')

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dest')
  EXEC('CREATE SCHEMA [dest]')

```
