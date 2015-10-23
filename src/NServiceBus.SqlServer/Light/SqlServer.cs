namespace NServiceBus.Transports.SQLServer.Light
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Settings;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServer : TransportDefinition
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public SqlServer()
        {
            //HINT: this flag indicates that user need to explicitly turn outbox in configuration.
            RequireOutboxConsent = true;
        }

        /// <summary>
        /// Gives implementations access to the <see cref="T:NServiceBus.BusConfiguration"/> instance at configuration time.
        /// </summary>
        protected override void Configure(BusConfiguration config)
        {
            config.EnableFeature<SqlServerConfigurator>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            //HINT: here goes support for TTBR
            return new Type[0]{};
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
    }
}