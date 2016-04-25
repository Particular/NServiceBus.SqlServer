namespace NServiceBus.Transports.SQLServer
{
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;

    interface ITableBasedQueue
    {
        string TransportAddress { get; }
        Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction);
        Task SendMessage(OutgoingMessage message, SqlConnection connection, SqlTransaction transaction);
        Task SendRawMessage(object[] data, SqlConnection connection, SqlTransaction transaction);
        Task<int> TryPeek(SqlConnection connection, CancellationToken token);
        Task<int> Purge(SqlConnection connection);
        Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize);
        Task LogWarningWhenIndexIsMissing(SqlConnection connection);
    }
}