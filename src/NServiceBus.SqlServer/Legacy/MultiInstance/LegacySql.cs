namespace NServiceBus.Transport.SQLServer
{
    class LegacySql
    {
        internal const string CreateQueueText = @"IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))
                  BEGIN
                    EXEC sp_getapplock @Resource = '{0}_lock', @LockMode = 'Exclusive'

                    IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE {0}(
                            [Id] [uniqueidentifier] NOT NULL,
                            [CorrelationId] [varchar](255),
                            [ReplyToAddress] [varchar](255),
                            [Recoverable] [bit] NOT NULL,
                            [Expires] [datetime],
                            [Headers] [varchar](max) NOT NULL,
                            [Body] [varbinary](max),
                            [RowVersion] [bigint] IDENTITY(1,1) NOT NULL
                        );

                        CREATE CLUSTERED INDEX [Index_RowVersion] ON {0}
                        (
                            [RowVersion]
                        )

                        CREATE NONCLUSTERED INDEX [Index_Expires] ON {0}
                        (
                            [Expires]
                        )
                        INCLUDE
                        (
                            [Id],
                            [RowVersion]
                        )
                    END

                    EXEC sp_releaseapplock @Resource = '{0}_lock'
                  END";
    }
}
