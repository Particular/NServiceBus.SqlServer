namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;

    class SqlServerQueueCreator : ICreateQueues
    {
        const string SchemaDdl = 
@"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{0}')
BEGIN
    EXEC('CREATE SCHEMA {0}');
END";

        const string TableDdl =
            @"IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))
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
                    
                  END";

        public void CreateQueueIfNecessary(Address address, string account)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    if (SchemaName != null)
                    {
                        EnsureSchemaExists(connection, transaction);
                    }
                    EnsureTableExists(address, connection, transaction);
                    transaction.Commit();
                }
            }
        }

        void EnsureTableExists(Address address, SqlConnection connection, SqlTransaction transaction)
        {
            var sql = string.Format(TableDdl, address.Queue.GetTableName(SchemaName ?? "dbo"));
            ExecuteCommand(connection, transaction, sql);
        }

        void EnsureSchemaExists(SqlConnection connection, SqlTransaction transaction)
        {
            var sql = string.Format(SchemaDdl, SchemaName);
            ExecuteCommand(connection, transaction, sql);
        }

        static void ExecuteCommand(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            using (var command = new SqlCommand(sql, connection)
            {
                Transaction = transaction,
                CommandType = CommandType.Text
            })
            {
                command.ExecuteNonQuery();
            }
        }

        public string ConnectionString { get; set; }
        public string SchemaName { get; set; }
    }
}