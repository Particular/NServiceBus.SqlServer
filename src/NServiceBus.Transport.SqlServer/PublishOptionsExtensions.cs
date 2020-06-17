namespace NServiceBus.Transport.SqlServer
{
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using Extensibility;
    using Transport;

    /// <summary>
    /// Adds transport specific settings to PublishOptions
    /// </summary>
    public static class PublishOptionsExtensions
    {
        /// <summary>
        /// Enables the use of custom SqlTransaction instances for publish operations. The same transaction can be used in more than one publish operation.
        /// </summary>
        /// <param name="options">The <see cref="PublishOptions" /> to extend.</param>
        /// <param name="transaction">SqlTransaction instance that will be used by any operations performed by the transport.</param>
        public static void UseCustomSqlTransaction(this PublishOptions options, SqlTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, transaction.Connection);
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, transaction);
            options.GetExtensions().Set(transportTransaction);
        }
    }
}