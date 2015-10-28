using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Extensibility;

namespace NServiceBus.Transports.SQLServer.Light
{
    using System.Data.SqlClient;

    class SqlServerMessageSender : IDispatchMessages
    {
        TableBasedQueue queue;
        readonly string connectionString;

        public SqlServerMessageSender(TableBasedQueue queue, string connectionString)
        {
            this.queue = queue;
            this.connectionString = connectionString;
        }

        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ContextBag context)
        {
            foreach (var outgoingMessage in outgoingMessages)
            {
                using (var sqlConnection = new SqlConnection(this.connectionString))
                {
                    sqlConnection.Open();
                    queue.SendMessage(sqlConnection, outgoingMessage);
                }
            }

            return Task.FromResult(0);
        }
    }
}