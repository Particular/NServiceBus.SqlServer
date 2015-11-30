namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Transactions;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Settings;

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

        /// <summary>
        /// <see cref="TransportDefinition.ConfigureForReceiving"/>
        /// </summary>
        protected override void ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            var schemaSpecifiedInCode = context.Settings.GetOrDefault<string>(ConnectionParams.DefaultSchemaSettingsKey);
            var connectionParams = new ConnectionParams(context.ConnectionString, schemaSpecifiedInCode);

            context.SetQueueCreatorFactory(() => new SqlServerQueueCreator(connectionParams));
            
            var transactionOptions = new TransactionOptions
            {
                IsolationLevel = context.Settings.Get<IsolationLevel>("Transactions.IsolationLevel"),
                Timeout = context.Settings.Get<TimeSpan>("Transactions.DefaultTimeout")
            }; 

            context.SetMessagePumpFactory(c => new MessagePump(c, guarantee => SelectReceiveStrategy(guarantee, transactionOptions, connectionParams.ConnectionString), connectionParams));
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
            var schemaSpecifiedInCode = context.GlobalSettings.GetOrDefault<string>(ConnectionParams.DefaultSchemaSettingsKey);
            var connectionParams = new ConnectionParams(context.ConnectionString, schemaSpecifiedInCode);

            context.SetDispatcherFactory(() => new SqlServerMessageDispatcher(connectionParams));
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
            return null;
        }

        /// <summary>
        /// <see cref="TransportDefinition.ToTransportAddress"/>.
        /// </summary>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var endpointName = logicalAddress.EndpointInstanceName.EndpointName.ToString();
            var qualifier = logicalAddress.Qualifier;
            var userDiscriminator = logicalAddress.EndpointInstanceName.UserDiscriminator;

            var nonEmptyParts = new[]
            {
                endpointName,
                qualifier,
                userDiscriminator
            }.Where(p => !string.IsNullOrEmpty(p));
            
            var transportAddress = string.Join(".", nonEmptyParts);

            return transportAddress;
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