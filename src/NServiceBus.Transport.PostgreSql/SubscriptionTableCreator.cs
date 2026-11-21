namespace NServiceBus.Transport.PostgreSql
{
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Npgsql;
    using NServiceBus.Transport.Sql.Shared;

    class SubscriptionTableCreator(ISqlConstants sqlConstants, QualifiedSubscriptionTableName tableName, PostgreSqlDbConnectionFactory connectionFactory)
    {
        static ILog Logger = LogManager.GetLogger<SubscriptionTableCreator>();

        public async Task CreateIfNecessary(CancellationToken cancellationToken = default)
        {
            var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
            await using var connectionDisposer = connection.ConfigureAwait(false);
            try
            {
                var transaction = connection.BeginTransaction();
                await using var transactionDisposer = transaction.ConfigureAwait(false);
                var sql = string.Format(sqlConstants.CreateSubscriptionTableText, tableName.QuotedQualifiedName,
                    tableName.QuotedCatalog);

                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.Transaction = transaction;
                    command.CommandText = sql;
                    command.CommandType = CommandType.Text;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                transaction.Commit();
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.DuplicateTable or  // 42P07
                PostgresErrorCodes.DuplicateObject or  // 42710
                PostgresErrorCodes.UniqueViolation)    // 23505
            {
                //This happens if the table creation is executed concurrently by multiple transactions
                //In this case we want to discard the exception and continue
                Logger.Debug("Subscription Table already exists.", ex);
            }
        }
    }
}