/* Drop all non-system stored procs */
DECLARE @name VARCHAR(128)
DECLARE @SQL VARCHAR(254)

SELECT @name = (SELECT TOP 1 [name] FROM sysobjects WHERE [type] = 'P' AND category = 0 ORDER BY [name])

WHILE @name is not null
BEGIN
    SELECT @SQL = 'DROP PROCEDURE [dbo].' + QUOTENAME(RTRIM(@name))
    EXEC (@SQL)
    PRINT 'Dropped Procedure: ' + @name
    SELECT @name = (SELECT TOP 1 [name] FROM sysobjects WHERE [type] = 'P' AND category = 0 AND [name] > @name ORDER BY [name])
END
GO

/* Drop all views */
DECLARE @name NVARCHAR(128)
DECLARE @SQL NVARCHAR(254)
DECLARE @schema NVARCHAR(128)

SELECT TOP 1 @name = sys.objects.name, @schema = sys.schemas.name FROM sys.objects INNER JOIN sys.schemas ON sys.objects.schema_id = sys.schemas.schema_id WHERE [type] = 'V' ORDER BY sys.objects.name
--SELECT @name = (SELECT TOP 1 [name] FROM sysobjects WHERE [type] = 'V' AND category = 0 ORDER BY [name])

WHILE @name IS NOT NULL
BEGIN
    SELECT @SQL = 'DROP VIEW ' + QUOTENAME(@schema) + '.' + QUOTENAME(RTRIM(@name))
    EXEC (@SQL)
    PRINT 'Dropped View: ' + @schema + '.' + @name
	SELECT  @name = NULL
	SELECT TOP 1 @name = sys.objects.name, @schema = sys.schemas.name FROM sys.objects INNER JOIN sys.schemas ON sys.objects.schema_id = sys.schemas.schema_id WHERE [type] = 'V' ORDER BY sys.objects.name
END
GO

/* Drop all functions */
DECLARE @name VARCHAR(128)
DECLARE @SQL VARCHAR(254)

SELECT @name = (SELECT TOP 1 [name] FROM sysobjects WHERE [type] IN (N'FN', N'IF', N'TF', N'FS', N'FT') AND category = 0 ORDER BY [name])

WHILE @name IS NOT NULL
BEGIN
    SELECT @SQL = 'DROP FUNCTION [dbo].' + QUOTENAME(RTRIM(@name))
    EXEC (@SQL)
    PRINT 'Dropped Function: ' + @name
    SELECT @name = (SELECT TOP 1 [name] FROM sysobjects WHERE [type] IN (N'FN', N'IF', N'TF', N'FS', N'FT') AND category = 0 AND [name] > @name ORDER BY [name])
END
GO

/* Drop all Foreign Key constraints */
DECLARE @name NVARCHAR(128)
DECLARE @schema NVARCHAR(128)
DECLARE @constraint NVARCHAR(254)
DECLARE @SQL NVARCHAR(254)

SELECT TOP 1 @name = TABLE_NAME, @schema = CONSTRAINT_SCHEMA, @constraint = CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE constraint_catalog=DB_NAME() AND CONSTRAINT_TYPE = 'FOREIGN KEY' ORDER BY TABLE_NAME

WHILE @name is not null
BEGIN
    SELECT @SQL = 'ALTER TABLE ' + QUOTENAME(@schema) + '.' + QUOTENAME(RTRIM(@name)) +' DROP CONSTRAINT ' + QUOTENAME(RTRIM(@constraint))
    EXEC (@SQL)
    PRINT 'Dropped FK Constraint: ' + @constraint + ' on ' + @schema + '.' + @name
	SELECT  @name = NULL
	SELECT TOP 1 @name = TABLE_NAME, @schema = CONSTRAINT_SCHEMA, @constraint = CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE constraint_catalog=DB_NAME() AND CONSTRAINT_TYPE = 'FOREIGN KEY' ORDER BY TABLE_NAME
END
GO

/* Drop all Primary Key constraints */
DECLARE @name NVARCHAR(128)
DECLARE @schema NVARCHAR(128)
DECLARE @constraint NVARCHAR(254)
DECLARE @SQL NVARCHAR(254)

SELECT TOP 1 @name = TABLE_NAME, @schema = CONSTRAINT_SCHEMA, @constraint = CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE constraint_catalog=DB_NAME() AND CONSTRAINT_TYPE = 'PRIMARY KEY' ORDER BY TABLE_NAME

WHILE @name IS NOT NULL
BEGIN
    SELECT @SQL = 'ALTER TABLE ' + QUOTENAME(@schema) + '.' + QUOTENAME(RTRIM(@name)) +' DROP CONSTRAINT ' + QUOTENAME(RTRIM(@constraint))
    EXEC (@SQL)
    PRINT 'Dropped PK Constraint: ' + @constraint + ' on ' + @schema + '.' + @name
    SELECT  @name = NULL
	SELECT TOP 1 @name = TABLE_NAME, @schema = CONSTRAINT_SCHEMA, @constraint = CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE constraint_catalog=DB_NAME() AND CONSTRAINT_TYPE = 'PRIMARY KEY' ORDER BY TABLE_NAME
END
GO

/* Drop all tables */
DECLARE @name NVARCHAR(128)
DECLARE @schema NVARCHAR(128)
DECLARE @SQL NVARCHAR(254)

SELECT TOP 1 @name = sys.objects.name, @schema = sys.schemas.name FROM sys.objects INNER JOIN sys.schemas ON sys.objects.schema_id = sys.schemas.schema_id WHERE [type] = 'U' ORDER BY sys.objects.name

WHILE @name IS NOT NULL
BEGIN
    SELECT @SQL = 'DROP TABLE ' + QUOTENAME(@schema) + '.' + QUOTENAME(RTRIM(@name))
    EXEC (@SQL)
    PRINT 'Dropped Table: ' + @schema + '.' + @name
	SELECT  @name = NULL
    SELECT TOP 1 @name = sys.objects.name, @schema = sys.schemas.name FROM sys.objects INNER JOIN sys.schemas ON sys.objects.schema_id = sys.schemas.schema_id WHERE [type] = 'U' ORDER BY sys.objects.name
END
GO

/* Create schemas */
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'nsb')
BEGIN
	EXEC('CREATE SCHEMA nsb')
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sender')
BEGIN
	EXEC('CREATE SCHEMA sender')
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'receiver')
BEGIN
	EXEC('CREATE SCHEMA receiver')
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'db@')
BEGIN
	EXEC('CREATE SCHEMA db@')
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'src')
BEGIN
	EXEC('CREATE SCHEMA src')
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dest')
BEGIN
	EXEC('CREATE SCHEMA dest')
END
GO

/*Create error queue to avoid race conditions*/
CREATE TABLE error (
    Id uniqueidentifier NOT NULL,
    CorrelationId varchar(255),
    ReplyToAddress varchar(255),
    Recoverable bit NOT NULL,
    Expires datetime,
    Headers nvarchar(max) NOT NULL,
    BodyString as cast(Body AS nvarchar(max)),
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