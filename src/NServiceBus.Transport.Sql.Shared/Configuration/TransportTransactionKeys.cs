namespace NServiceBus.Transport.Sql.Shared
{
    class TransportTransactionKeys
    {
        // For backward compatibility reasons these settings keys are hard coded to the System.Data types to enable connection and transaction sharing with SQL persistence
        public const string SqlConnection = "System.Data.SqlClient.SqlConnection";
        public const string SqlTransaction = "System.Data.SqlClient.SqlTransaction";

        public const string IsUserProvidedTransaction = "SqlServer.Transaction.IsUserProvided";
    }
}