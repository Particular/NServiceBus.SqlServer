namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using System.Transactions.Configuration;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;
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

        QueueAddressProvider CreateAddressParser(ReadOnlySettings settings)
        {
            string defaultSchemaOverride;
            Func<string, string> schemaOverrider;

            settings.TryGet(SettingsKeys.DefaultSchemaSettingsKey, out defaultSchemaOverride);
            settings.TryGet(SettingsKeys.SchemaOverrideCallbackSettingsKey, out schemaOverrider);

            var parser = new QueueAddressProvider("dbo", defaultSchemaOverride, schemaOverrider);

            return parser;
        }

        SqlConnectionFactory CreateConnectionFactory(string connectionString, ReadOnlySettings settings)
        {
            Func<Task<SqlConnection>> factoryOverride;

            if (settings.TryGet(SettingsKeys.ConnectionFactoryOverride, out factoryOverride))
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

            SqlScopeOptions scopeOptions;
            if (!context.Settings.TryGet(out scopeOptions))
            {
                scopeOptions = new SqlScopeOptions();
            }

            TimeSpan waitTimeCircuitBreaker;

            if (!context.Settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggering, out waitTimeCircuitBreaker))
            {
                waitTimeCircuitBreaker = TimeSpan.FromSeconds(30);
            }

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory = guarantee => SelectReceiveStrategy(guarantee, scopeOptions.TransactionOptions, connectionFactory);

            return new TransportReceivingConfigurationResult(
                () => new MessagePump(receiveStrategyFactory, connectionFactory, addressParser, waitTimeCircuitBreaker),
                () => new QueueCreator(connectionFactory, addressParser),
                () => Task.FromResult(StartupCheckResult.Success));
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
        /// <see cref="TransportDefinition.ConfigureForSending"/>
        /// </summary>
        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var connectionFactory = CreateConnectionFactory(context.ConnectionString, context.Settings);
            var parser = CreateAddressParser(context.Settings);

            return new TransportSendingConfigurationResult(
                () => new MessageDispatcher(connectionFactory, parser),
                () =>
                {
                    var result = UsingV2ConfigurationChecker.Check();
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
        /// <see cref="TransportDefinition.GetSupportedTransactionMode"/>.
        /// </summary>
        public override TransportTransactionMode GetSupportedTransactionMode()
        {
            return TransportTransactionMode.TransactionScope;
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetSubscriptionManager"/>.
        /// </summary>
        public override IManageSubscriptions GetSubscriptionManager()
        {
            throw new NotSupportedException("Sql don't support native pub sub");
        }

        /// <summary>
        /// Returns the discriminator for this endpoint instance.
        /// </summary>
        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance, ReadOnlySettings settings)
        {
            return instance.SetProperty(SchemaPropertyKey, CreateAddressParser(settings).DefaultSchema);
        }

        /// <summary>
        /// <see cref="TransportDefinition.ToTransportAddress"/>.
        /// </summary>
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

        const string SchemaPropertyKey = "Schema";

        internal class SqlScopeOptions
        {
            public TransactionOptions TransactionOptions { get; }

            public SqlScopeOptions(TimeSpan? requestedTimeout = null, IsolationLevel? requestedIsolationLevel = null)
            {
                var isolationLevel = IsolationLevel.ReadCommitted;
                if (requestedIsolationLevel.HasValue)
                {
                    isolationLevel = requestedIsolationLevel.Value;
                }

                var timeout = TransactionManager.DefaultTimeout;
                if (requestedTimeout.HasValue)
                {
                    var maxTimeout = GetMaxTimeout();

                    if (requestedTimeout.Value > maxTimeout)
                    {
                        throw new ConfigurationErrorsException(
                            "Timeout requested is longer than the maximum value for this machine. Please override using the maxTimeout setting of the system.transactions section in machine.config");
                    }

                    timeout = requestedTimeout.Value;
                }

                TransactionOptions = new TransactionOptions
                {
                    IsolationLevel = isolationLevel,
                    Timeout = timeout
                };
            }

            private static TimeSpan GetMaxTimeout()
            {
                //default is always 10 minutes
                var maxTimeout = TimeSpan.FromMinutes(10);

                var systemTransactionsGroup = ConfigurationManager.OpenMachineConfiguration()
                    .GetSectionGroup("system.transactions");

                var machineSettings = systemTransactionsGroup?.Sections.Get("machineSettings") as MachineSettingsSection;

                if (machineSettings != null)
                {
                    maxTimeout = machineSettings.MaxTimeout;
                }

                return maxTimeout;
            }
        }
    }
}