namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Transports.SQLServer;

    /// <summary>
    /// SqlServer Transport Infrastructure
    /// </summary>
    internal class SqlServerTransportInfrastructure : TransportInfrastructure
    {
        internal SqlServerTransportInfrastructure(QueueAddressParser addressParser, SettingsHolder settings, string connectionString)
        {
            this.addressParser = addressParser;
            this.settings = settings;
            this.connectionString = connectionString;

            //HINT: this flag indicates that user need to explicitly turn outbox in configuration.
            RequireOutboxConsent = true;
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureReceiveInfrastructure"/>
        /// </summary>
        /// <returns></returns>
        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            var connectionFactory = CreateConnectionFactory();

            SqlScopeOptions scopeOptions;
            if (!settings.TryGet(out scopeOptions))
            {
                scopeOptions = new SqlScopeOptions();
            }

            TimeSpan waitTimeCircuitBreaker;

            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggering, out waitTimeCircuitBreaker))
            {
                waitTimeCircuitBreaker = TimeSpan.FromSeconds(30);
            }

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory = 
                guarantee => SelectReceiveStrategy(guarantee, scopeOptions.TransactionOptions, connectionFactory);

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, connectionFactory, addressParser, waitTimeCircuitBreaker),
                () => new QueueCreator(connectionFactory, addressParser),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        SqlConnectionFactory CreateConnectionFactory()
        {
            Func<Task<SqlConnection>> factoryOverride;

            if (settings.TryGet(SettingsKeys.ConnectionFactoryOverride, out factoryOverride))
            {
                return new SqlConnectionFactory(factoryOverride);
            }

            return SqlConnectionFactory.Default(connectionString);
        }

        ReceiveStrategy SelectReceiveStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
        {
            if (minimumConsistencyGuarantee == TransportTransactionMode.TransactionScope)
            {
                return new ReceiveWithTransactionScope(options, connectionFactory);
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.SendsAtomicWithReceive)
            {
                return new ReceiveWithSendsAtomicWithReceiveTransaction(options, connectionFactory);
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.ReceiveOnly)
            {
                return new ReceiveWithReceiveOnlyTransaction(options, connectionFactory);
            }

            return new ReceiveWithNoTransaction(connectionFactory);
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureSendInfrastructure"/>
        /// </summary>
        /// <returns></returns>
        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var connectionFactory = CreateConnectionFactory();

            return new TransportSendInfrastructure(
                () => new MessageDispatcher(connectionFactory, addressParser),
                () =>
                {
                    var result = UsingV2ConfigurationChecker.Check();
                    return Task.FromResult(result);
                });
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureSubscriptionInfrastructure"/>
        /// </summary>
        /// <returns></returns>
        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.BindToLocalEndpoint"/>
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            return instance.SetProperty(SchemaPropertyKey, addressParser.DefaultSchema);
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.ToTransportAddress"/>
        /// </summary>
        /// <param name="logicalAddress"></param>
        /// <returns></returns>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var nonEmptyParts = new[]
            {
                logicalAddress.EndpointInstance.Endpoint.ToString(),
                logicalAddress.Qualifier,
                logicalAddress.EndpointInstance.Discriminator
            }.Where(p => !string.IsNullOrEmpty(p));

            var address = string.Join(".", nonEmptyParts);

            string schemaName;

            if (logicalAddress.EndpointInstance.Properties.TryGetValue(SchemaPropertyKey, out schemaName))
            {
                address += $"@{schemaName}";
            }

            return address;
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.MakeCanonicalForm"/>
        /// </summary>
        /// <param name="transportAddress"></param>
        /// <returns></returns>
        public override string MakeCanonicalForm(string transportAddress)
        {
            return addressParser.Parse(transportAddress).ToString();
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.DeliveryConstraints"/>
        /// </summary>
        public override IEnumerable<Type> DeliveryConstraints { get; } = new[] { typeof(DiscardIfNotReceivedBefore) };

        /// <summary>
        /// <see cref="TransportInfrastructure.TransactionMode"/>
        /// </summary>
        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.TransactionScope;

        /// <summary>
        /// <see cref="TransportInfrastructure.OutboundRoutingPolicy"/>
        /// </summary>
        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

        const string SchemaPropertyKey = "Schema";

        QueueAddressParser addressParser;
        SettingsHolder settings;
        string connectionString;
    }
}