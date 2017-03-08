#pragma warning disable 1591
namespace NServiceBus.Transport.SQLServer
{
    using System;

    /// <summary>
    /// Not for public use.
    /// </summary>
    [Obsolete("Not for public use.")]
    public static class SqlConstants
    {
        public static readonly string PurgeText = "DELETE FROM {0}.{1}";

        public static readonly string SendText =
            @"
DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON'
SET NOCOUNT ON;

INSERT INTO {0}.{1} ([Id], [CorrelationId], [ReplyToAddress], [Recoverable], [Expires], [Headers], [Body])
VALUES (@Id, @CorrelationId, @ReplyToAddress, @Recoverable, CASE WHEN @TimeToBeReceivedMs IS NOT NULL THEN DATEADD(ms, @TimeToBeReceivedMs, GETUTCDATE()) END, @Headers, @Body);

IF (@NOCOUNT = 'ON') SET NOCOUNT ON;
IF (@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        public static readonly string ReceiveText = @"
DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON';
SET NOCOUNT ON;

WITH message AS (SELECT TOP(1) * FROM {0}.{1} WITH (UPDLOCK, READPAST, ROWLOCK) WHERE [Expires] IS NULL OR [Expires] > GETUTCDATE() ORDER BY [RowVersion])
DELETE FROM message
OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, deleted.Recoverable, deleted.Headers, deleted.Body;
IF (@NOCOUNT = 'ON') SET NOCOUNT ON;
IF (@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        public static readonly string PeekText = "SELECT count(*) Id FROM {0}.{1} WITH (READPAST) WHERE [Expires] IS NULL OR [Expires] > GETUTCDATE();";

        public static readonly string CreateQueueText = @"
IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}].[{1}]') AND type in (N'U'))
BEGIN
    EXEC sp_getapplock @Resource = '{0}_{1}_lock', @LockMode = 'Exclusive'

    IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}].[{1}]') AND type in (N'U'))
    BEGIN
        CREATE TABLE [{0}].[{1}](
            [Id] [uniqueidentifier] NOT NULL,
            [CorrelationId] [varchar](255) NULL,
            [ReplyToAddress] [varchar](255) NULL,
            [Recoverable] [bit] NOT NULL,
            [Expires] [datetime] NULL,
            [Headers] [varchar](max) NOT NULL,
            [Body] [varbinary](max) NULL,
            [RowVersion] [bigint] IDENTITY(1,1) NOT NULL
        ) ON [PRIMARY];

        CREATE CLUSTERED INDEX [Index_RowVersion] ON [{0}].[{1}]
        (
            [RowVersion] ASC
        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]

        CREATE NONCLUSTERED INDEX [Index_Expires] ON [{0}].[{1}]
        (
            [Expires] ASC
        )
        INCLUDE
        (
            [Id],
            [RowVersion]
        )
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
    END

    EXEC sp_releaseapplock @Resource = '{0}_{1}_lock'
END";

        public static readonly string PurgeBatchOfExpiredMessagesText = "DELETE FROM {1}.{2} WHERE [RowVersion] IN (SELECT TOP ({0}) [RowVersion] FROM {1}.{2} WITH (NOLOCK) WHERE [Expires] < GETUTCDATE())";

        public static readonly string CheckIfExpiresIndexIsPresent = @"SELECT COUNT(*) FROM [sys].[indexes] WHERE [name] = 'Index_Expires' AND [object_id] = OBJECT_ID('{0}.{1}')";
        
    }
}