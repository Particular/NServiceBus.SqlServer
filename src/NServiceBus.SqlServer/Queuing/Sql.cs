namespace NServiceBus.Transport.SQLServer
{
    class Sql
    {
        internal const string PurgeText = "DELETE FROM {0}";

        internal const string SendText =
            @"
              DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
              IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON'
              SET NOCOUNT ON;

              INSERT INTO {0} ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body])
              VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,CASE WHEN @TimeToBeReceivedMs IS NOT NULL THEN DATEADD(ms, @TimeToBeReceivedMs, GETUTCDATE()) END,@Headers,@Body);;

              IF(@NOCOUNT = 'ON') SET NOCOUNT ON;
              IF(@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        internal const string ReceiveText = @"
            DECLARE @NOCOUNT VARCHAR(3) = 'OFF';
            IF ( (512 & @@OPTIONS) = 512 ) SET @NOCOUNT = 'ON';
            SET NOCOUNT ON;

            WITH message AS (SELECT TOP(1) * FROM {0} WITH (UPDLOCK, READPAST, ROWLOCK) WHERE [Expires] IS NULL OR [Expires] > GETUTCDATE() ORDER BY [RowVersion])
            DELETE FROM message
            OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, deleted.Recoverable, deleted.Headers, deleted.Body;
            IF(@NOCOUNT = 'ON') SET NOCOUNT ON;
            IF(@NOCOUNT = 'OFF') SET NOCOUNT OFF;";

        internal const string PeekText = "SELECT count(*) Id FROM {0} WITH (READPAST) WHERE [Expires] IS NULL OR [Expires] > GETUTCDATE();";

        internal const string CreateQueueText = @"IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))
                  BEGIN
                    EXEC sp_getapplock @Resource = '{1}_lock', @LockMode = 'Exclusive'

                    IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE {0}(
                            [Id] [uniqueidentifier] NOT NULL,
                            [CorrelationId] [varchar](255) NULL,
                            [ReplyToAddress] [varchar](255) NULL,
                            [Recoverable] [bit] NOT NULL,
                            [Expires] [datetime] NULL,
                            [Headers] [varchar](max) NOT NULL,
                            [Body] [varbinary](max) NULL,
                            [RowVersion] [bigint] IDENTITY(1,1) NOT NULL
                        ) ON [PRIMARY];

                        CREATE CLUSTERED INDEX [Index_RowVersion] ON {0}
                        (
                            [RowVersion] ASC
                        )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]

                        CREATE NONCLUSTERED INDEX [Index_Expires] ON {0}
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

                    EXEC sp_releaseapplock @Resource = '{1}_lock'
                  END";

        internal const string PurgeBatchOfExpiredMessagesText = "DELETE FROM {1} WHERE [RowVersion] IN (SELECT TOP ({0}) [RowVersion] FROM {1} WITH (NOLOCK) WHERE [Expires] < GETUTCDATE())";

        internal const string CheckIfExpiresIndexIsPresent = @"SELECT COUNT(*) FROM [sys].[indexes] WHERE [name] = '{0}' AND [object_id] = OBJECT_ID('{1}')";

        internal const string ExpiresIndexName = "Index_Expires";
    }
}
