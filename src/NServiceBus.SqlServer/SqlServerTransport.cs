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
        /// Ctor
        /// </summary>
        public SqlServerTransport()
        {
            //HINT: this flag indicates that user need to explicitly turn outbox in configuration.
            RequireOutboxConsent = true;
        }

        /// <summary>
        /// Registers components necessary for receiving messages from the transport.
        /// </summary>
        /// <param name="context"></param>
        protected override void ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            var connectionParams = new ConnectionParams(context.ConnectionString);

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
        /// Registers components necessary for sending messages.
        /// </summary>
        /// <param name="context"></param>
        protected override void ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var connectionParams = new ConnectionParams(context.ConnectionString);

            context.SetDispatcherFactory(() => new SqlServerMessageSender(connectionParams));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            return new[]
            {
                typeof(DiscardIfNotReceivedBefore)
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override TransactionSupport GetTransactionSupport()
        {
            return TransactionSupport.Distributed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override IManageSubscriptions GetSubscriptionManager()
        {
            throw new NotSupportedException("Sql don't support native pub sub");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string GetDiscriminatorForThisEndpointInstance()
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logicalAddress"></param>
        /// <returns></returns>
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
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            return new OutboundRoutingPolicy(OutboundRoutingType.DirectSend, OutboundRoutingType.DirectSend, OutboundRoutingType.DirectSend);
        }

        /// <summary>
        /// Sample connection string.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";

        /// <summary>
        /// Specifies if connection string is required for SqlServer transport.
        /// </summary>
        public override bool RequiresConnectionString => true;
    }
}