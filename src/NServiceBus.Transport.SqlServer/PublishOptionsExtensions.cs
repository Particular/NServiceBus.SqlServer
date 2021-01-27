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
            // When dispatching, the TransportTransaction is overwritten.
            // The only way for a custom transaction to work is by using immediate dispatch and messages should only appear when the user commits the custom transaction.
            // Which is exactly what will happen after NServiceBus dispatches this message immediately.
            options.RequireImmediateDispatch();

            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(SettingsKeys.IsUserProvidedTransactionKey, true);
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, transaction);
            options.GetExtensions().Set(transportTransaction);
        }

        /// <summary>
        /// Enables the use of custom SqlConnection for publish operations.
        /// </summary>
        /// <param name="options">The <see cref="PublishOptions" /> to extend.</param>
        /// <param name="connection">SqlConnection instance that will be used by any operations performed by the transport.</param>
        public static void UseCustomSqlConnection(this PublishOptions options, SqlConnection connection)
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