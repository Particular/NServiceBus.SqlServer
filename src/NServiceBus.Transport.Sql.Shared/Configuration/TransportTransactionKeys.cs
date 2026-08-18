namespace NServiceBus.Transport.Sql.Shared
{
    static class TransportTransactionKeys
    {
        // For backward compatibility reasons these settings keys are hard coded to the System.Data types to enable connection and transaction sharing with SQL persistence
        public const string SqlConnection = "System.Data.SqlClient.SqlConnection";
        public const string SqlTransaction = "System.Data.SqlClient.SqlTransaction";

        public const string IsUserProvidedTransaction = "SqlServer.Transaction.IsUserProvided";

        // Well-known key read by downstream components (e.g. SQL persistence) to detect that they must not reuse the receive connection and transaction
        public const string ReceiveOnlyTransactionMode = "SqlTransport.ReceiveOnlyTransactionMode";
    }
}
