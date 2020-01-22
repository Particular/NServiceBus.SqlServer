namespace NServiceBus.Transport.SQLServer
{
    using System.Data;
    using System.Threading.Tasks;

    class SubscriptionTableCreator
    {
        QualifiedSubscriptionTableName tableName;
        SqlConnectionFactory connectionFactory;

        public SubscriptionTableCreator(QualifiedSubscriptionTableName tableName, SqlConnectionFactory connectionFactory)
        {
            this.tableName = tableName;
            this.connectionFactory = connectionFactory;
        }
        public async Task CreateIfNecessary()
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
#pragma warning disable 618
                var sql = string.Format(SqlConstants.CreateSubscriptionTableText, tableName.QuotedQualifiedName, tableName.QuotedCatalog);
#pragma warning restore 618
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;
                    command.Connection = connection;
                    command.Transaction = transaction;
                    
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                transaction.Commit();
            }
        }
    }
}