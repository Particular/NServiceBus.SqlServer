namespace NServiceBus.Transports.SQLServer.Light
{
    using System;
    using System.Collections.Generic;
    using System.Transactions;
    using Settings;

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
            var connectionString = context.ConnectionString;

            context.SetQueueCreatorFactory(() => new SqlServerQueueCreator(connectionString));

            //TODO: this is a smell. Figure out how to get this data in a static type manner
            var transactionOptions = new TransactionOptions
            {
                IsolationLevel = context.Settings.Get<IsolationLevel>("Transactions.IsolationLevel"),
                Timeout = context.Settings.Get<TimeSpan>("Transactions.DefaultTimeout")
            }; 

            context.SetMessagePumpFactory(c => new MessagePump(c, guarantee => SelectReceiveStrategy(guarantee, transactionOptions, connectionString), connectionString));
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

            return new ReceiveWithNativeTransaction(connectionString);
        }

        /// <summary>
        /// Registers components necessary for sending messages.
        /// </summary>
        /// <param name="context"></param>
        protected override void ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var connectionString = context.ConnectionString;

            context.SetDispatcherFactory(() => new SqlServerMessageSender(new TableBasedQueue("", "", connectionString), connectionString));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            //HINT: here goes support for TTBR
            return new Type[]{};
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
            //TODO: check what that is hmm..
            return Environment.MachineName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logicalAddress"></param>
        /// <returns></returns>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            //TODO: check what that is hmm..
            var endpointName = logicalAddress.EndpointInstanceName.EndpointName.ToString();
            var qualifier = logicalAddress.Qualifier;

            if (string.IsNullOrEmpty(qualifier))
            {
                return $"{endpointName}";
            }

            return $"{endpointName}.{qualifier}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            //TODO: check what that does 
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