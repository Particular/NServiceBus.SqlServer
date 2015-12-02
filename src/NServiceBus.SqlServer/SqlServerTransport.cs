namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
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

        /// <summary>
        /// <see cref="TransportDefinition.ConfigureForReceiving"/>
        /// </summary>
        protected override void ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            var connectionString = context.ConnectionString;
            var addressParser = CreateAddressParser(context.Settings);

            context.SetQueueCreatorFactory(() => new SqlServerQueueCreator(connectionString, addressParser));
            
            var transactionOptions = new TransactionOptions
            {
                IsolationLevel = context.Settings.Get<IsolationLevel>("Transactions.IsolationLevel"),
                Timeout = context.Settings.Get<TimeSpan>("Transactions.DefaultTimeout")
            };

            Func<TransactionSupport, ReceiveStrategy> receiveStrategyFactory = guarantee => SelectReceiveStrategy(guarantee, transactionOptions, connectionString);

            context.SetMessagePumpFactory(c => new MessagePump(c, receiveStrategyFactory, connectionString, addressParser));
        }

        ReceiveStrategy SelectReceiveStrategy(TransactionSupport minimumConsistencyGuarantee, TransactionOptions options, string connectionString)
        {
            if (minimumConsistencyGuarantee == TransactionSupport.Distributed)
            {
                return new ReceiveWithTransactionScope(options, connectionString);
            }

            if (minimumConsistencyGuarantee == TransactionSupport.None)
            {
                return new ReceiveWithNoTransaction(connectionString);
            }

            return new ReceiveWithNativeTransaction(options, connectionString);
        }

        /// <summary>
        /// <see cref="TransportDefinition.ConfigureForSending"/>
        /// </summary>
        protected override void ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var connectionString = context.ConnectionString;
            var parser = CreateAddressParser(context.GlobalSettings);

            context.SetDispatcherFactory(() => new SqlServerMessageDispatcher(connectionString, parser));
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
            return new OutboundRoutingPolicy(OutboundRoutingType.DirectSend, OutboundRoutingType.DirectSend, OutboundRoutingType.DirectSend);
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