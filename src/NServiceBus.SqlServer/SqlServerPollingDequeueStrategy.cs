namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using CircuitBreakers;
    using Janitor;
    using Logging;
    using NServiceBus.Features;
    using Pipeline;
    using Unicast.Transport;

    /// <summary>
    ///     A polling implementation of <see cref="IDequeueMessages" />.
    /// </summary>
    class SqlServerPollingDequeueStrategy : IDequeueMessages, IDisposable
    {
        public SqlServerPollingDequeueStrategy(PipelineExecutor pipelineExecutor, CriticalError criticalError, Configure config, SecondaryReceiveConfiguration secondaryReceiveConfiguration)
        {
            this.pipelineExecutor = pipelineExecutor;
            this.secondaryReceiveConfiguration = secondaryReceiveConfiguration;
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SqlTransportConnectivity",
                TimeSpan.FromMinutes(2),
                ex => criticalError.Raise("Repeated failures when communicating with SqlServer", ex),
                TimeSpan.FromSeconds(10));
            purgeOnStartup = config.PurgeOnStartup();
        }

        /// <summary>
        ///     The connection used to open the SQL Server database.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        ///     Initializes the <see cref="IDequeueMessages" />.
        /// </summary>
        /// <param name="address">The address to listen on.</param>
        /// <param name="transactionSettings">
        ///     The <see cref="TransactionSettings" /> to be used by <see cref="IDequeueMessages" />.
        /// </param>
        /// <param name="tryProcessMessage">Called when a message has been dequeued and is ready for processing.</param>
        /// <param name="endProcessMessage">
        ///     Needs to be called by <see cref="IDequeueMessages" /> after the message has been processed regardless if the
        ///     outcome was successful or not.
        /// </param>
        public void Init(Address address, TransactionSettings transactionSettings,
            Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            this.endProcessMessage = endProcessMessage;

            var receiveStrategyFactory = new ReceiveStrategyFactory(tryProcessMessage, new TransportMessageReader(), ConnectionString, pipelineExecutor, transactionSettings);
            receiveStrategy = receiveStrategyFactory.ChooseReceiveStrategy();

            primaryAddress = address;
            secondaryReceiveSettings = secondaryReceiveConfiguration.GetSettings(primaryAddress.Queue);

            if (purgeOnStartup)
            {
                PurgeTable(AllTables());
            }
        }

        IEnumerable<string> AllTables()
        {
            yield return primaryAddress.GetTableName();
            if (SecondaryReceiveSettings.IsEnabled)
            {
                yield return SecondaryReceiveSettings.ReceiveQueue;
            }
        }


        /// <summary>
        ///     Starts the dequeuing of message using the specified <paramref name="maximumConcurrencyLevel" />.
        /// </summary>
        /// <param name="maximumConcurrencyLevel">
        ///     Indicates the maximum concurrency level this <see cref="IDequeueMessages" /> is able to support.
        /// </param>
        public void Start(int maximumConcurrencyLevel)
        {
            var actualConcurrencyLevel = maximumConcurrencyLevel + SecondaryReceiveSettings.MaximumConcurrencyLevel;
            tokenSource = new CancellationTokenSource();

            // We need to add an extra one because if we fail and the count is at zero already, it doesn't allow us to add one more.
            countdownEvent = new CountdownEvent(actualConcurrencyLevel + 1);

            for (var i = 0; i < maximumConcurrencyLevel; i++)
            {
                StartReceiveThread(primaryAddress.GetTableName());
            }
            for (var i = 0; i < SecondaryReceiveSettings.MaximumConcurrencyLevel; i++)
            {
                StartReceiveThread(SecondaryReceiveSettings.ReceiveQueue.GetTableName());
            }
            if (SecondaryReceiveSettings.IsEnabled)
            {
                Logger.InfoFormat("Secondary receiver for queue '{0}' initiated with concurrency '{1}'", SecondaryReceiveSettings.ReceiveQueue, SecondaryReceiveSettings.MaximumConcurrencyLevel);
            }

        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
            if (tokenSource == null)
            {
                return;
            }

            tokenSource.Cancel();
            countdownEvent.Signal();
            countdownEvent.Wait();
        }

        public void Dispose()
        {
            // Injected
        }

        void PurgeTable(IEnumerable<string> tableNames)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                foreach (var tableName in tableNames)
                {
                    using (var command = new SqlCommand(string.Format(SqlPurge, tableName), connection)
                    {
                        CommandType = CommandType.Text
                    })
                    {
                        var numberOfPurgedRows = command.ExecuteNonQuery();

                        Logger.InfoFormat("{0} messages was purged from table {1}", numberOfPurgedRows, tableName);
                    }
                }
            }
        }

        void StartReceiveThread(string tableName)
        {
            var token = tokenSource.Token;

            Task.Factory
                .StartNew(ReceiveLoop, new ReceiveLoppArgs(token, tableName), token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t =>
                {
                    t.Exception.Handle(ex =>
                    {
                        Logger.Warn("An exception occurred when connecting to the configured SqlServer", ex);
                        circuitBreaker.Failure(ex);
                        return true;
                    });

                    if (!tokenSource.IsCancellationRequested)
                    {
                        if (countdownEvent.TryAddCount())
                        {
                            StartReceiveThread(tableName);
                        }
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        class ReceiveLoppArgs
        {
            public readonly CancellationToken Token;
            public readonly string TableName;

            public ReceiveLoppArgs(CancellationToken token, string tableName)
            {
                Token = token;
                TableName = tableName;
            }
        }

        void ReceiveLoop(object obj)
        {
            try
            {
                var args = (ReceiveLoppArgs)obj;
                var backOff = new BackOff(1000);
                var query = string.Format(SqlReceive, args.TableName);

                while (!args.Token.IsCancellationRequested)
                {
                    var result = new ReceiveResult();

                    try
                    {
                        result = receiveStrategy.TryReceive(query);
                    }
                    finally
                    {
                        //since we're polling the message will be null when there was nothing in the queue
                        if (result.Message != null)
                        {
                            endProcessMessage(result.Message, result.Exception);
                        }
                    }

                    circuitBreaker.Success();
                    backOff.Wait(() => result.Message == null);
                }
            }
            finally
            {
                countdownEvent.Signal();
            }
        }

        SecondaryReceiveSettings SecondaryReceiveSettings
        {
            get
            {
                if (secondaryReceiveSettings == null)
                {
                    throw new InvalidOperationException("Cannot get secondary receive settings before Init was called.");
                }
                return secondaryReceiveSettings;
            }
        }

        const string SqlReceive =
            @"WITH message AS (SELECT TOP(1) * FROM [{0}] WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowVersion] ASC) 
			DELETE FROM message 
			OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, 
			deleted.Recoverable, deleted.Expires, deleted.Headers, deleted.Body;";

        const string SqlPurge = @"DELETE FROM [{0}]";

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerPollingDequeueStrategy));

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        CountdownEvent countdownEvent;
        Action<TransportMessage, Exception> endProcessMessage;
        [SkipWeaving]
        readonly PipelineExecutor pipelineExecutor;

        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;
        IReceiveStrategy receiveStrategy;
        SecondaryReceiveSettings secondaryReceiveSettings;
        bool purgeOnStartup;
        Address primaryAddress;
        CancellationTokenSource tokenSource;
    }
}