namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Transports.SQLServer;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServerTransport : TransportDefinition
    {
        /// <summary>
        /// Initializes a new insatnce of <see cref="SqlServerTransport"/>.
        /// </summary>
        public SqlServerTransport()
        {
            //HINT: this flag indicates that user need to explicitly turn outbox in configuration.
            RequireOutboxConsent = true;
        }

        SqlServerAddressProvider CreateAddressParser(ReadOnlySettings settings)
        {
            string defaultSchemaOverride;
            Func<string, string> schemaOverrider;

            settings.TryGet(SqlServerSettingsKeys.DefaultSchemaSettingsKey, out defaultSchemaOverride);
            settings.TryGet(SqlServerSettingsKeys.SchemaOverrideCallbackSettingsKey, out schemaOverrider);

            var parser = new SqlServerAddressProvider("dbo", defaultSchemaOverride, schemaOverrider);

            return parser;
        }

        SqlConnectionFactory CreateConnectionFactory(string connectionString, ReadOnlySettings settings)
        {
            Func<Task<SqlConnection>> factoryOverride;

            if (settings.TryGet(SqlServerSettingsKeys.ConnectionFactoryOverride, out factoryOverride))
            {
                return new SqlConnectionFactory(factoryOverride);
            }

            return SqlConnectionFactory.Default(connectionString);
        }

        /// <summary>
        /// <see cref="TransportDefinition.ConfigureForReceiving"/>
        /// </summary>
        protected override TransportReceivingConfigurationResult ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            var connectionFactory = CreateConnectionFactory(context.ConnectionString, context.Settings);
            var addressParser = CreateAddressParser(context.Settings);
            
            var transactionOptions = new TransactionOptions
            {
                IsolationLevel = context.Settings.Get<IsolationLevel>("Transactions.IsolationLevel"),
                Timeout = context.Settings.Get<TimeSpan>("Transactions.DefaultTimeout")
            };

            TimeSpan waitTimeCircuitBreaker;
            if(context.Settings.TryGet(CircuitBreakerSettingsKeys.TimeToWaitBeforeTriggering, out waitTimeCircuitBreaker) == false)
                waitTimeCircuitBreaker = TimeSpan.FromSeconds(30);

            Func<TransactionSupport, ReceiveStrategy> receiveStrategyFactory = guarantee => SelectReceiveStrategy(guarantee, transactionOptions, connectionFactory);

            return new TransportReceivingConfigurationResult(
                c => new MessagePump(c, receiveStrategyFactory, connectionFactory, addressParser, waitTimeCircuitBreaker),
                () => new SqlServerQueueCreator(connectionFactory, addressParser),
                () => { return Task.FromResult(StartupCheckResult.Success); });
        }

        ReceiveStrategy SelectReceiveStrategy(TransactionSupport minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
        {
            if (minimumConsistencyGuarantee == TransactionSupport.Distributed)
            {
                return new ReceiveWithTransactionScope(options, connectionFactory);
            }

            if (minimumConsistencyGuarantee == TransactionSupport.None)
            {
                return new ReceiveWithNoTransaction(connectionFactory);
            }

            return new ReceiveWithNativeTransaction(options, connectionFactory);
        }

        /// <summary>
        /// <see cref="TransportDefinition.ConfigureForSending"/>
        /// </summary>
        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var connectionFactory = CreateConnectionFactory(context.ConnectionString, context.Settings);
            var parser = CreateAddressParser(context.Settings);

            return new TransportSendingConfigurationResult(
                () => new SqlServerMessageDispatcher(connectionFactory, parser),
                () => {
                        var result = UsingOldConfigurationCheck.Check();
                        return Task.FromResult(result);
                      });
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetSupportedDeliveryConstraints"/>.
        /// </summary>
        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            return new[]
            {
                typeof(DiscardIfNotReceivedBefore)
            };
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetTransactionSupport"/>.
        /// </summary>
        public override TransactionSupport GetTransactionSupport()
        {
            return TransactionSupport.Distributed;
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetSubscriptionManager"/>.
        /// </summary>
        public override IManageSubscriptions GetSubscriptionManager()
        {
            throw new NotSupportedException("Sql don't support native pub sub");
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetDiscriminatorForThisEndpointInstance"/>.
        /// </summary>
        public override string GetDiscriminatorForThisEndpointInstance(ReadOnlySettings settings)
        {
            return CreateAddressParser(settings).DefaultSchema;
        }

        /// <summary>
        /// <see cref="TransportDefinition.ToTransportAddress"/>.
        /// </summary>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            return SqlServerAddress.ToTransportAddressText(logicalAddress);
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetOutboundRoutingPolicy"/>.
        /// </summary>
        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            return new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
        }

        /// <summary>
        /// <see cref="TransportDefinition.ExampleConnectionStringForErrorMessage"/>
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";

        /// <summary>
        /// <see cref="TransportDefinition.RequiresConnectionString"/>
        /// </summary>
        public override bool RequiresConnectionString => true;
    }
}