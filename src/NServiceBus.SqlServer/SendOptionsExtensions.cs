namespace NServiceBus.Transport.SQLServer
{
    using System.Data.SqlClient;
    using Extensibility;

    /// <summary>
    /// Adds transport specific settings to SendOptions
    /// </summary>
    public static class SendOptionsExtensions
    {
        /// <summary>
        /// Enables providing <see cref="SqlConnection"/> and <see cref="SqlTransaction"/> instances that will be used by send operations. The same connection and transaction
        /// can be used in more than one send operation.
        /// </summary>
        /// <param name="options">The <see cref="SendOptions" /> to extend.</param>
        /// <param name="connection">Open <see cref="SqlConnection"/> instance to be used by send operations.</param>
        /// <param name="transaction"><see cref="SqlTransaction"/> instance that will be used by any operations performed by the transport.</param>
        public static void UseCustomSqlConnectionAndTransaction(this SendOptions options, SqlConnection connection, SqlTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(connection);
            transportTransaction.Set(transaction);

            options.GetExtensions().Set(transportTransaction);
        }
    }
}