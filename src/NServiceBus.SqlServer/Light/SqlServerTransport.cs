namespace NServiceBus.Transports.SQLServer.Light
{
    using System;
    using System.Collections.Generic;
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

            context.SetMessagePumpFactory(c => new MessagePump(c, SelectReceiveStrategy, connectionString));
        }

        ReceiveStrategy SelectReceiveStrategy(TransactionSupport minimalGuarantees)
        {
            //TODO: add support different transaction quarantees. Remmber about transaction options from the settings
            return new NoTransactionReceiveStrategy();
        }

        /// <summary>
        /// Registers components necessary for sending messages.
        /// </summary>
        /// <param name="context"></param>
        protected override void ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var connectionString = context.ConnectionString;

            context.SetDispatcherFactory(() => new SqlServerMessageSender(new TableBasedQueue("", "", connectionString)));
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