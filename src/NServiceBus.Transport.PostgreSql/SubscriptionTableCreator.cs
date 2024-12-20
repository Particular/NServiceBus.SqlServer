namespace NServiceBus.Transport.PostgreSql
{
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Npgsql;
    using NServiceBus.Transport.Sql.Shared;

    class SubscriptionTableCreator
    {
        ISqlConstants sqlConstants;
        QualifiedSubscriptionTableName tableName;
        PostgreSqlDbConnectionFactory connectionFactory;
        static ILog Logger = LogManager.GetLogger<SubscriptionTableCreator>();

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
                        var sql = string.Format(sqlConstants.CreateSubscriptionTableText, tableName.QuotedQualifiedName,
                            tableName.QuotedCatalog);

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
                catch (PostgresException ex) when (ex.SqlState == "23505")
                {
                    //PostgreSQL error code 23505: unique_violation is returned
                    //if the table creation is executed concurrently by multiple transactions
                    //In this case we want to discard the exception and continue
                    Logger.Debug("Subscription Table already exists.", ex);
                }
            }
        }
    }
}