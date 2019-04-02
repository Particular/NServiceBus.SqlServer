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
        [ObsoleteEx(
            RemoveInVersion = "6.0",
            TreatAsErrorFromVersion = "5.0",
            ReplacementTypeOrMember = "UseCustomSqlTransaction",
            Message = "The connection parameter is no longer required.")]
        public static void UseCustomSqlConnectionAndTransaction(this SendOptions options, SqlConnection connection, SqlTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(connection);
            transportTransaction.Set(transaction);

            options.GetExtensions().Set(transportTransaction);
        }

        /// <summary>
        /// Enables the use of custom SqlTransaction instances for send operations. The same transaction can be used in more than one send operation.
        /// </summary>
        /// <param name="options">The <see cref="SendOptions" /> to extend.</param>
        /// <param name="transaction">SqlTransaction instance that will be used by any operations performed by the transport.</param>
        public static void UseCustomSqlTransaction(this SendOptions options, SqlTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(transaction.Connection);
            transportTransaction.Set(transaction);

            options.GetExtensions().Set(transportTransaction);
        }
    }
}