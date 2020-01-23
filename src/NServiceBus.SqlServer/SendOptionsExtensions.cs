namespace NServiceBus.Transport.SQLServer
{
#if !MSSQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using Extensibility;

    /// <summary>
    /// Adds transport specific settings to SendOptions
    /// </summary>
    public static partial class SendOptionsExtensions
    {
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