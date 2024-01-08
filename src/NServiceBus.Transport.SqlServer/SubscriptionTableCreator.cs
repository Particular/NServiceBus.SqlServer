namespace NServiceBus.Transport.SqlServer
{
    using System.Data;
    using System.Threading.Tasks;
    using System.Threading;
    using Npgsql;

    class SubscriptionTableCreator
    {
        QualifiedSubscriptionTableName tableName;
        SqlConnectionFactory connectionFactory;

        public SubscriptionTableCreator(QualifiedSubscriptionTableName tableName, SqlConnectionFactory connectionFactory)
        {
            this.tableName = tableName;
            this.connectionFactory = connectionFactory;
        }
        public async Task CreateIfNecessary(CancellationToken cancellationToken = default)
        {
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        var sql = string.Format(SqlConstants.CreateSubscriptionTableText, tableName.QuotedQualifiedName, tableName.QuotedCatalog);

                        using (var command = new NpgsqlCommand(sql, connection, transaction)
                        {
                            CommandType = CommandType.Text
                        })
                        {
                            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        transaction.Commit();
                    }
                }
                catch (NpgsqlException e) when (e.ErrorCode is 2714 or 1913) //Object already exists
                {
                    //Table creation scripts are based on sys.objects metadata views.
                    //It looks that these views are not fully transactional and might
                    //not return information on already created table under heavy load.
                    //This in turn can result in executing table create or index create queries
                    //for objects that already exists. These queries will fail with
                    // 2714 (table) and 1913 (index) error codes.
                }
            }
        }
    }
}