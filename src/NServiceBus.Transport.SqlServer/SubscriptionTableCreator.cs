namespace NServiceBus.Transport.SqlServer
{
    using System.Data;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using System.Threading;

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

                        using (var command = new SqlCommand(sql, connection, transaction)
                        {
                            CommandType = CommandType.Text
                        })
                        {
                            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        transaction.Commit();
                    }
                }
                catch (SqlException e) when (e.Number == 2714 || e.Number == 1913) //Object already exists
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