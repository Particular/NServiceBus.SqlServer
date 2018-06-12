namespace NServiceBus.Transport.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class DelayedDeliveryQueueCreator : ICreateQueues
    {
        public DelayedDeliveryQueueCreator(SqlConnectionFactory connectionFactory, ICreateQueues queueCreator, CanonicalQueueAddress delayedMessageTable, bool createMessageBodyComputedColumn = false)
        {
            this.connectionFactory = connectionFactory;
            this.queueCreator = queueCreator;
            this.delayedMessageTable = delayedMessageTable;
            this.createMessageBodyComputedColumn = createMessageBodyComputedColumn;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            await queueCreator.CreateQueueIfNecessary(queueBindings, identity).ConfigureAwait(false);
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                await CreateDelayedMessageQueue(delayedMessageTable, connection, transaction, createMessageBodyComputedColumn).ConfigureAwait(false);
                transaction.Commit();
            }
        }

        static async Task CreateDelayedMessageQueue(CanonicalQueueAddress canonicalQueueAddress, SqlConnection connection, SqlTransaction transaction, bool createMessageBodyColumn)
        {
#pragma warning disable 618
            var messageBodyComputedColumn = createMessageBodyColumn ? SqlConstants.MessageBodyStringColumn : string.Empty;
            var sql = string.Format(SqlConstants.CreateDelayedMessageStoreText, canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName, messageBodyComputedColumn);
#pragma warning restore 618
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        SqlConnectionFactory connectionFactory;
        ICreateQueues queueCreator;
        CanonicalQueueAddress delayedMessageTable;
        readonly bool createMessageBodyComputedColumn;
    }
}