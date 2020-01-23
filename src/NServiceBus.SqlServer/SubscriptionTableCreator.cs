namespace NServiceBus.Transport.SQLServer
{
    using System.Data;
#if !MSSQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
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
                using (var command = new SqlCommand(sql, connection, transaction)
                {
                    CommandType = CommandType.Text
                })
                {
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                transaction.Commit();
            }
        }
    }
}