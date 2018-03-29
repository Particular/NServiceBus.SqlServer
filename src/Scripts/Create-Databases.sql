CREATE DATABASE nservicebus
GO
USE nservicebus
GO
CREATE SCHEMA nsb
GO
CREATE SCHEMA sender
GO
CREATE SCHEMA receiver
GO
CREATE SCHEMA db@
GO
CREATE SCHEMA src
GO
CREATE SCHEMA dest
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
    BodyString as cast(Body as nvarchar(max)),
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

CREATE DATABASE nservicebus1
GO
USE nservicebus1
GO
CREATE SCHEMA nsb
GO
CREATE SCHEMA sender
GO
CREATE SCHEMA receiver
GO
CREATE SCHEMA db@
GO
CREATE SCHEMA src
GO
CREATE SCHEMA dest
GO

CREATE DATABASE nservicebus2
GO
USE nservicebus2
GO
CREATE SCHEMA nsb
GO
CREATE SCHEMA sender
GO
CREATE SCHEMA receiver
GO
CREATE SCHEMA db@
GO
CREATE SCHEMA src
GO
CREATE SCHEMA dest
GO