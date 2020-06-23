namespace NServiceBus
{
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System;
    using Extensibility;
    using Transport;
    using Transport.SqlServer;

    /// <summary>
    /// Adds transport specific settings to SendOptions
    /// </summary>
    public static class SendOptionsExtensions
    {
        /// <summary>
        /// Enables the use of custom SqlTransaction instances for send operations. The same transaction can be used in more than one send operation.
        /// </summary>
        /// <param name="options">The <see cref="SendOptions" /> to extend.</param>
        /// <param name="transaction">SqlTransaction instance that will be used by any operations performed by the transport.</param>
        public static void UseCustomSqlTransaction(this SendOptions options, SqlTransaction transaction)
        {
            // When dispatching, the TransportTransaction is overwritten.
            // The only way for a custom transaction to work is by using immediate dispatch and messages should only appear when the user commits the custom transaction.
            // Which is exactly what will happen after NServiceBus dispatches this message immediately.
            options.RequireImmediateDispatch();

            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(SettingsKeys.IsUserProvidedTransactionKey, true);
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, transaction.Connection);
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, transaction);
            options.GetExtensions().Set(transportTransaction);
        }

        /// <summary>
        /// Enables the use of custom SqlConnection for send operations.
        /// </summary>
        /// <param name="options">The <see cref="SendOptions" /> to extend.</param>
        /// <param name="connection">SqlConnection instance that will be used by any operations performed by the transport.</param>
        public static void UseCustomSqlConnection(this SendOptions options, SqlConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentException(nameof(connection));
            }

            options.RequireImmediateDispatch();

            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(SettingsKeys.IsUserProvidedTransactionKey, true);
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);

            options.GetExtensions().Set(transportTransaction);
        }


    }
}