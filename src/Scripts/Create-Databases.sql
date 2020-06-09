SET QUOTED_IDENTIFIER  ON

IF NOT DB_ID('nservicebus') IS NOT NULL
  CREATE DATABASE [nservicebus]
GO
USE nservicebus
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

GO

/*Create error queue to avoid race conditions*/
CREATE TABLE error (
    Id uniqueidentifier NOT NULL,
    CorrelationId varchar(255),
    ReplyToAddress varchar(255),
    Recoverable bit NOT NULL,
    Expires datetime,
    Headers nvarchar(max) NOT NULL,
    Body varbinary(max),
    RowVersion bigint IDENTITY(1,1) NOT NULL
);

CREATE CLUSTERED INDEX Index_RowVersion ON error
(
    RowVersion
)

CREATE NONCLUSTERED INDEX Index_Expires ON error
(
    Expires
)
INCLUDE
(
    Id,
    RowVersion
)
WHERE
    Expires IS NOT NULL
GO

IF NOT DB_ID('nservicebus1') IS NOT NULL
  CREATE DATABASE [nservicebus1]
GO

USE nservicebus1
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'src')
  EXEC('CREATE SCHEMA [src]')

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dest')
  EXEC('CREATE SCHEMA [dest]')


IF NOT DB_ID('nservicebus2') IS NOT NULL
  CREATE DATABASE [nservicebus2]
GO
