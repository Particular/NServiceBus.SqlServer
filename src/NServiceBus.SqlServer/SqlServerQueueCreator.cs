namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;

    class SqlServerQueueCreator : ICreateQueues
    {
        const string Ddl =
            @"IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}].[{1}]') AND type in (N'U'))
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
                    )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                    
                  END";

        public void CreateQueueIfNecessary(Address address, string account)
        {
            var connectionParams = connectionStringProvider.GetForDestination(address);
            using (var connection = sqlConnectionFactory.OpenNewConnection(connectionParams.ConnectionString))
            {
                var sql = string.Format(Ddl, connectionParams.Schema, address.GetTableName());

                using (var command = new SqlCommand(sql, connection) {CommandType = CommandType.Text})
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        readonly IConnectionStringProvider connectionStringProvider;
        readonly CustomSqlConnectionFactory sqlConnectionFactory;

        public SqlServerQueueCreator(IConnectionStringProvider connectionStringProvider, CustomSqlConnectionFactory sqlConnectionFactory)
        {
            this.connectionStringProvider = connectionStringProvider;
            this.sqlConnectionFactory = sqlConnectionFactory;
        }
    }
}