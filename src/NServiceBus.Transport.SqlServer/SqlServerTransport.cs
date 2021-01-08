using System.Collections.Generic;
using System.Threading;
using NServiceBus.Routing;

namespace NServiceBus
{
    using System;
    using System.Data.Common;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using Settings;
    using Transport;
    using Transport.SqlServer;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServerTransport : TransportDefinition
    {
        string connectionString;
        Func<Task<SqlConnection>> connectionFactory;

        DbConnectionStringBuilder GetConnectionStringBuilder()
        {
            if (connectionFactory != null)
            {
                using (var connection = connectionFactory().GetAwaiter().GetResult())
                {
                    return new DbConnectionStringBuilder {ConnectionString = connection.ConnectionString};
                }
            }

            return new DbConnectionStringBuilder {ConnectionString = connectionString};
        }

        string GetDefaultCatalog()
        {
            var parser = GetConnectionStringBuilder();

            if (parser.TryGetValue("Initial Catalog", out var catalog) ||
                parser.TryGetValue("database", out catalog))
            {
                return (string)catalog;
            }

            throw new Exception("Initial Catalog property is mandatory in the connection string.");
        }

        bool IsEncrypted()
        {
            var parser = GetConnectionStringBuilder();

            if (parser.TryGetValue("Column Encryption Setting", out var enabled))
            {
                return ((string)enabled).Equals("enabled", StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Creates and instance of <see cref="SqlServerTransport"/>
        /// </summary>
        public SqlServerTransport(TransportTransactionMode defaultTransactionMode) : base(defaultTransactionMode)
        {
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize"/>
        /// </summary>
        public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (ConnectionFactory == null && string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception(
                    $"Either {nameof(ConnectionString)} or {nameof(ConnectionFactory)} property has to be specified in the SQL Server transport configuration.");
            }

            var catalog = GetDefaultCatalog();
            var isEncrypted = IsEncrypted();

            return new SqlServerTransportInfrastructure(catalog, settings, connectionString, settings.LocalAddress, settings.LogicalAddress, isEncrypted);
        }

        /// <summary>
        /// Translates a <see cref="QueueAddress"/> object into a transport specific queue address-string.
        /// </summary>
        public override string ToTransportAddress(Transport.QueueAddress address)
        {
            //TODO: remove dependency on the logical address
            var logicalAddress = LogicalAddress.CreateRemoteAddress(
                new EndpointInstance(address.BaseAddress, address.Discriminator, address.Properties));
            var fullAddress = logicalAddress.CreateQualifiedAddress(address.Qualifier);

            return QueueAddressTranslator.TranslateLogicalAddress(fullAddress).Value;
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetSupportedTransactionModes"/>
        /// </summary>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes()
        {
            return new[]
            {
                TransportTransactionMode.None,
                TransportTransactionMode.ReceiveOnly,
                TransportTransactionMode.SendsAtomicWithReceive,
                TransportTransactionMode.TransactionScope
            };
        }

        /// <summary>
        /// <see cref="TransportDefinition.SupportsDelayedDelivery"/>
        /// </summary>
        public override bool SupportsDelayedDelivery { get; } = true;

        /// <summary>
        /// <see cref="TransportDefinition.SupportsPublishSubscribe"/>
        /// </summary>
        public override bool SupportsPublishSubscribe { get; } = true;

        /// <summary>
        /// <see cref="TransportDefinition.SupportsTTBR"/>
        /// </summary>
        public override bool SupportsTTBR { get; } = true;

        /// <summary>
        /// Connection string to be used by the transport.
        /// </summary>
        public string ConnectionString
        {
            get => connectionString;
            set
            {
                if (ConnectionFactory != null)
                {
                    throw new ArgumentException($"{nameof(ConnectionFactory)} has already been set. {nameof(ConnectionString)} and {nameof(ConnectionFactory)} cannot be used at the same time.");
                }

                connectionString = value;
            }
        }


        /// <summary>
        /// Connection string factory.
        /// </summary>
        public Func<Task<SqlConnection>> ConnectionFactory {
            get => connectionFactory;
            set
            {
                if (string.IsNullOrWhiteSpace(ConnectionString) == false)
                {
                    throw new ArgumentException($"{nameof(ConnectionString)} has already been set. {nameof(ConnectionString)} and {nameof(ConnectionFactory)} cannot be used at the same time.");
                }

                connectionFactory = value;
            } 
        }
    }
}