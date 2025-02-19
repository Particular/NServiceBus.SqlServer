namespace NServiceBus.Transport.SqlServer
{
    using NServiceBus.Transport.Sql.Shared;

    class SqlServerConstants : ISqlConstants
    {
        public string PurgeText { get; set; } = "DELETE FROM {0}";

        public string SendTextWithRecoverable { get; set; } =
            @"
DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON'
SET NOCOUNT ON;

INSERT INTO {0} (
    Id,
    Recoverable,
    Expires,
    Headers,
    Body)
VALUES (
    @Id,
    1,
    CASE WHEN @TimeToBeReceivedMs IS NOT NULL
        THEN DATEADD(ms, @TimeToBeReceivedMs, GETUTCDATE()) END,
    @Headers,
    @Body);

IF (@NOCOUNT = 'ON') SET NOCOUNT ON;
IF (@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        public string SendTextWithoutRecoverable { get; set; } =
            @"
DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON'
SET NOCOUNT ON;

INSERT INTO {0} (
    Id,
    Expires,
    Headers,
    Body)
VALUES (
    @Id,
    CASE WHEN @TimeToBeReceivedMs IS NOT NULL
        THEN DATEADD(ms, @TimeToBeReceivedMs, GETUTCDATE()) END,
    @Headers,
    @Body);

IF (@NOCOUNT = 'ON') SET NOCOUNT ON;
IF (@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        public string CheckIfTableHasRecoverableText { get; set; } = @"
SELECT COUNT(*)
FROM [{0}].sys.columns c
WHERE c.object_id = OBJECT_ID(N'{1}')
    AND c.name = 'Recoverable'";

        public string StoreDelayedMessageText { get; set; } =
@"
DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON'
SET NOCOUNT ON;

DECLARE @DueAfter DATETIME = GETUTCDATE();
SET @DueAfter = DATEADD(ms, @DueAfterMilliseconds, @DueAfter);
SET @DueAfter = DATEADD(s, @DueAfterSeconds, @DueAfter);
SET @DueAfter = DATEADD(n, @DueAfterMinutes, @DueAfter);
SET @DueAfter = DATEADD(hh, @DueAfterHours, @DueAfter);
SET @DueAfter = DATEADD(d, @DueAfterDays, @DueAfter);

INSERT INTO {0} (
    Headers,
    Body,
    Due)
VALUES (
    @Headers,
    @Body,
    @DueAfter);

IF(@NOCOUNT = 'ON') SET NOCOUNT ON;
IF(@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        public string ReceiveText { get; set; } = @"
DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON';
SET NOCOUNT ON;

WITH message AS (
    SELECT TOP(1) *
    FROM {0} WITH (UPDLOCK, READPAST, ROWLOCK)
    ORDER BY RowVersion)
DELETE FROM message
OUTPUT
    deleted.Id,
    CASE WHEN deleted.Expires IS NULL
        THEN 0
        ELSE CASE WHEN deleted.Expires > GETUTCDATE()
            THEN 0
            ELSE 1
        END
    END,
    deleted.Headers,
    deleted.Body;

IF (@NOCOUNT = 'ON') SET NOCOUNT ON;
IF (@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        public string MoveDueDelayedMessageText { get; set; } = @"
;WITH message AS (
    SELECT TOP(@BatchSize) *
    FROM {0} WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE Due < GETUTCDATE())
DELETE FROM message
OUTPUT
    NEWID(),
    NULL,
    NULL,
    1,
    NULL,
    deleted.Headers,
    deleted.Body
INTO {1} (Id, CorrelationId, ReplyToAddress, Recoverable, Expires, Headers, Body);

SELECT TOP 1 GETUTCDATE() as UtcNow, Due as NextDue
FROM {0} WITH (READPAST)
ORDER BY Due";

        public string PeekText { get; set; } = @"
SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) Id FROM {0} WITH (READPAST, READCOMMITTEDLOCK)";

        public string AddMessageBodyStringColumn { get; set; } = @"
IF NOT EXISTS (
    SELECT *
    FROM {1}.sys.objects
    WHERE object_id = OBJECT_ID(N'{0}')
        AND type in (N'U'))
RETURN

IF EXISTS (
  SELECT *
  FROM   {1}.sys.columns
  WHERE  object_id = OBJECT_ID(N'{0}')
         AND name = 'BodyString'
)
RETURN

EXEC sp_getapplock @Resource = '{0}_lock', @LockMode = 'Exclusive'

IF EXISTS (
  SELECT *
  FROM   {1}.sys.columns
  WHERE  object_id = OBJECT_ID(N'{0}')
         AND name = 'BodyString'
)
BEGIN
    EXEC sp_releaseapplock @Resource = '{0}_lock'
    RETURN
END

ALTER TABLE {0}
ADD BodyString as cast(Body as nvarchar(max));

EXEC sp_releaseapplock @Resource = '{0}_lock'";

        public string CreateQueueText { get; set; } = @"
IF EXISTS (
    SELECT *
    FROM {1}.sys.objects
    WHERE object_id = OBJECT_ID(N'{0}')
        AND type in (N'U'))
RETURN

EXEC sp_getapplock @Resource = '{0}_lock', @LockMode = 'Exclusive'

IF EXISTS (
    SELECT *
    FROM {1}.sys.objects
    WHERE object_id = OBJECT_ID(N'{0}')
        AND type in (N'U'))
BEGIN
    EXEC sp_releaseapplock @Resource = '{0}_lock'
    RETURN
END

BEGIN TRY
    CREATE TABLE {0} (
        Id uniqueidentifier NOT NULL,
        CorrelationId varchar(255),
        ReplyToAddress varchar(255),
        Recoverable bit NOT NULL,
        Expires datetime,
        Headers nvarchar(max) NOT NULL,
        Body varbinary(max),
        RowVersion bigint IDENTITY(1,1) NOT NULL
    );

    CREATE NONCLUSTERED INDEX Index_RowVersion ON {0}
    (
        [RowVersion] ASC
    )

    CREATE NONCLUSTERED INDEX Index_Expires ON {0}
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
END TRY
BEGIN CATCH
    EXEC sp_releaseapplock @Resource = '{0}_lock';
    THROW;
END CATCH;

EXEC sp_releaseapplock @Resource = '{0}_lock'";

        public string CreateDelayedMessageStoreText { get; set; } = @"
IF EXISTS (
    SELECT *
    FROM {1}.sys.objects
    WHERE object_id = OBJECT_ID(N'{0}')
        AND type in (N'U'))
RETURN

EXEC sp_getapplock @Resource = '{0}_lock', @LockMode = 'Exclusive'

IF EXISTS (
    SELECT *
    FROM {1}.sys.objects
    WHERE object_id = OBJECT_ID(N'{0}')
        AND type in (N'U'))
BEGIN
    EXEC sp_releaseapplock @Resource = '{0}_lock'
    RETURN
END

BEGIN TRY
    CREATE TABLE {0} (
        Headers nvarchar(max) NOT NULL,
        Body varbinary(max),
        Due datetime NOT NULL,
        RowVersion bigint IDENTITY(1,1) NOT NULL
    );

    CREATE NONCLUSTERED INDEX [Index_Due] ON {0}
    (
        [Due]
    )
END TRY
BEGIN CATCH
    EXEC sp_releaseapplock @Resource = '{0}_lock';
    THROW;
END CATCH;

EXEC sp_releaseapplock @Resource = '{0}_lock'";

        public string PurgeBatchOfExpiredMessagesText { get; set; } = @"
DELETE FROM {0}
WHERE RowVersion
    IN (SELECT TOP (@BatchSize) RowVersion
        FROM {0} WITH (READPAST)
        WHERE Expires < GETUTCDATE())";

        public string CheckIfExpiresIndexIsPresent { get; set; } = @"
SELECT COUNT(*)
FROM sys.indexes i
INNER JOIN sys.index_columns AS ic ON ic.index_id = i.index_id AND ic.object_id = i.object_id AND ic.key_ordinal = 1
INNER JOIN sys.columns AS c ON c.column_id = ic.column_id AND c.object_id = ic.object_id
WHERE i.object_id = OBJECT_ID('{0}')
AND c.name = 'Expires'";

        public string CheckIfNonClusteredRowVersionIndexIsPresent { get; set; } = @"
SELECT COUNT(*)
FROM sys.indexes i
INNER JOIN sys.index_columns AS ic ON ic.index_id = i.index_id AND ic.object_id = i.object_id AND ic.key_ordinal = 1
INNER JOIN sys.columns AS c ON c.column_id = ic.column_id AND c.object_id = ic.object_id
WHERE i.object_id = OBJECT_ID('{0}')
AND c.name = 'RowVersion'
AND i.type = 2";

        public string CheckHeadersColumnType { get; set; } = @"
SELECT t.name
FROM sys.columns c
INNER JOIN sys.types t ON c.system_type_id = t.system_type_id
WHERE c.object_id = OBJECT_ID('{0}')
    AND c.name = 'Headers'";

        public string CreateSubscriptionTableText { get; set; } = @"

IF EXISTS (
    SELECT *
    FROM {1}.sys.objects
    WHERE object_id = OBJECT_ID(N'{0}')
        AND type in (N'U'))
RETURN

EXEC sp_getapplock @Resource = '{0}_lock', @LockMode = 'Exclusive'

IF EXISTS (
    SELECT *
    FROM {1}.sys.objects
    WHERE object_id = OBJECT_ID(N'{0}')
        AND type in (N'U'))
BEGIN
    EXEC sp_releaseapplock @Resource = '{0}_lock'
    RETURN
END

BEGIN TRY
    CREATE TABLE {0} (
        QueueAddress NVARCHAR(200) NOT NULL,
        Endpoint NVARCHAR(200) NOT NULL,
        Topic NVARCHAR(200) NOT NULL,
        PRIMARY KEY CLUSTERED
        (
            Endpoint,
            Topic
        )
    )
END TRY
BEGIN CATCH
    EXEC sp_releaseapplock @Resource = '{0}_lock';
    THROW;
END CATCH;

EXEC sp_releaseapplock @Resource = '{0}_lock'";

        public string SubscribeText { get; set; } = @"
MERGE {0} WITH (HOLDLOCK, TABLOCK) AS target
USING(SELECT @Endpoint AS Endpoint, @QueueAddress AS QueueAddress, @Topic AS Topic) AS source
ON target.Endpoint = source.Endpoint
AND target.Topic = source.Topic
WHEN MATCHED AND target.QueueAddress <> source.QueueAddress THEN
UPDATE SET QueueAddress = @QueueAddress
WHEN NOT MATCHED THEN
INSERT
(
    QueueAddress,
    Topic,
    Endpoint
)
VALUES
(
    @QueueAddress,
    @Topic,
    @Endpoint
);";

        public string GetSubscribersText { get; set; } = @"
SELECT DISTINCT QueueAddress
FROM {0}
WHERE Topic IN ({1})
";

        public string UnsubscribeText { get; set; } = @"
DELETE FROM {0}
WHERE
    Endpoint = @Endpoint and
    Topic = @Topic";

    }
}