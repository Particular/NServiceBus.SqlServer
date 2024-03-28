namespace NServiceBus.Transport.PostgreSql
{
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Sql.Shared.Queuing;

    class SubscriptionTableCreator
    {
        ISqlConstants sqlConstants;
        QualifiedSubscriptionTableName tableName;
        PostgreSqlDbConnectionFactory connectionFactory;

        public SubscriptionTableCreator(ISqlConstants sqlConstants, QualifiedSubscriptionTableName tableName, PostgreSqlDbConnectionFactory connectionFactory)
        {
            this.sqlConstants = sqlConstants;
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
                        var sql = string.Format(sqlConstants.CreateSubscriptionTableText, tableName.QuotedQualifiedName, tableName.QuotedCatalog);

                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = sql;
                            command.CommandType = CommandType.Text;

                            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        transaction.Commit();
                    }
                }
                //TODO: should figure out the error code or if we can prevent that altogether with PostreSQL
                catch (NpgsqlException e) when (e.ErrorCode is 2714 or 1913) //Object already exists
                {
                    //Table creation scripts are based on sys.objects metadata views.
                    //It looks that these views are not fully transactional and might
                    //not return information on already created table under heavy load.
                    //This in turn can result in executing table create or index create queries
                    //for objects that already exists. These queries will fail with
                    // 2714 (table) and 1913 (index) error codes.
                }
                catch (PostgresException ex) when (ex.SqlState == "23505")
                {
                    //PostgreSQL error code 23505: unique_violation is returned
                    //if the table creation is executed concurrently by multiple transactions
                    //In this case we want to discard the exception and continue
                }
            }
        }
    }
}