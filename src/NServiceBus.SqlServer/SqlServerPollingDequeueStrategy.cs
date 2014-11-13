namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using CircuitBreakers;
    using Janitor;
    using Logging;
    using NServiceBus.Features;
    using Pipeline;
    using Unicast.Transport;
    using IsolationLevel = System.Data.IsolationLevel;

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
        /// The name of the error queue
        /// </summary>
        public Address ErrorQueue { get; set; }

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
            this.tryProcessMessage = tryProcessMessage;
            this.endProcessMessage = endProcessMessage;

            settings = transactionSettings;
            transactionOptions = new TransactionOptions
            {
                IsolationLevel = transactionSettings.IsolationLevel,
                Timeout = transactionSettings.TransactionTimeout
            };
            primaryAddress = address;
            secondaryReceiveSettings = secondaryReceiveConfiguration.GetSettings(primaryAddress.Queue);
            errorQueue = new TableBasedQueue(ErrorQueue);

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
                StartReceiveThread(new TableBasedQueue(primaryAddress));
            }
            for (var i = 0; i < SecondaryReceiveSettings.MaximumConcurrencyLevel; i++)
            {
                StartReceiveThread(new TableBasedQueue(SecondaryReceiveSettings.ReceiveQueue.GetTableName()));
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

        void StartReceiveThread(TableBasedQueue queue)
        {
            var token = tokenSource.Token;

            Task.Factory
                .StartNew(ReceiveLoop, new ReceiveLoppArgs(token, queue), token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
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
                            StartReceiveThread(queue);
                        }
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        class ReceiveLoppArgs
        {
            public readonly CancellationToken Token;
            public readonly TableBasedQueue Queue;

            public ReceiveLoppArgs(CancellationToken token, TableBasedQueue queue)
            {
                Token = token;
                Queue = queue;
            }
        }

        void ReceiveLoop(object obj)
        {
            try
            {
                var args = (ReceiveLoppArgs)obj;
                var backOff = new BackOff(1000);

                while (!args.Token.IsCancellationRequested)
                {
                    var result = new ReceiveResult();

                    try
                    {
                        if (settings.IsTransactional)
                        {
                            if (settings.SuppressDistributedTransactions)
                            {
                                result = TryReceiveWithNativeTransaction(args.Queue);
                            }
                            else
                            {
                                result = TryReceiveWithTransactionScope(args.Queue);
                            }
                        }
                        else
                        {
                            result = TryReceiveWithNoTransaction(args.Queue);
                        }
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

        ReceiveResult TryReceiveWithNoTransaction(TableBasedQueue queue)
        {
            var result = new ReceiveResult();
            MessageReadResult readResult;
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                readResult = queue.TryReceive(connection);
                if (readResult.IsPoison)
                {
                    errorQueue.Send(readResult.DataRecord, connection);
                    return result;
                }
            }

            if (!readResult.Successful)
            {
                return result;
            }

            result.Message = readResult.Message;
            try
            {
                tryProcessMessage(readResult.Message);
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }

            return result;
        }

        ReceiveResult TryReceiveWithTransactionScope(TableBasedQueue queue)
        {
            var result = new ReceiveResult();

            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    try
                    {
                        connection.Open();
                        pipelineExecutor.CurrentContext.Set(string.Format("SqlConnection-{0}", ConnectionString), connection);

                        var readResult = queue.TryReceive(connection);
                        if (readResult.IsPoison)
                        {
                            errorQueue.Send(readResult.DataRecord, connection);
                            scope.Complete();
                            return result;
                        }

                        if (!readResult.Successful)
                        {
                            scope.Complete();
                            return result;
                        }

                        result.Message = readResult.Message;

                        try
                        {
                            if (tryProcessMessage(readResult.Message))
                            {
                                scope.Complete();
                                scope.Dispose(); // We explicitly calling Dispose so that we force any exception to not bubble, eg Concurrency/Deadlock exception.
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Exception = ex;
                        }

                        return result;
                    }
                    finally
                    {
                        pipelineExecutor.CurrentContext.Remove(string.Format("SqlConnection-{0}", ConnectionString));
                    }
                }
            }
        }

        ReceiveResult TryReceiveWithNativeTransaction(TableBasedQueue queue)
        {
            var result = new ReceiveResult();

            using (var connection = new SqlConnection(ConnectionString))
            {
                try
                {
                    pipelineExecutor.CurrentContext.Set(string.Format("SqlConnection-{0}", ConnectionString), connection);

                    connection.Open();

                    using (var transaction = connection.BeginTransaction(GetSqlIsolationLevel(settings.IsolationLevel)))
                    {
                        try
                        {
                            pipelineExecutor.CurrentContext.Set(string.Format("SqlTransaction-{0}", ConnectionString), transaction);

                            MessageReadResult readResult;
                            try
                            {
                                readResult = queue.TryReceive(connection, transaction);
                            }
                            catch (Exception)
                            {
                                transaction.Rollback();
                                throw;
                            }

                            if (readResult.IsPoison)
                            {
                                errorQueue.Send(readResult.DataRecord, connection, transaction);
                                transaction.Commit();
                                return result;
                            }

                            if (!readResult.Successful)
                            {
                                transaction.Commit();
                                return result;
                            }

                            result.Message = readResult.Message;

                            try
                            {
                                if (tryProcessMessage(readResult.Message))
                                {
                                    transaction.Commit();
                                }
                                else
                                {
                                    transaction.Rollback();
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Exception = ex;
                                transaction.Rollback();
                            }

                            return result;
                        }
                        finally
                        {
                            pipelineExecutor.CurrentContext.Remove(string.Format("SqlTransaction-{0}", ConnectionString));
                        }
                    }
                }
                finally
                {
                    pipelineExecutor.CurrentContext.Remove(string.Format("SqlConnection-{0}", ConnectionString));
                }
            }
        }

        static IsolationLevel GetSqlIsolationLevel(System.Transactions.IsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case System.Transactions.IsolationLevel.Serializable:
                    return IsolationLevel.Serializable;
                case System.Transactions.IsolationLevel.RepeatableRead:
                    return IsolationLevel.RepeatableRead;
                case System.Transactions.IsolationLevel.ReadCommitted:
                    return IsolationLevel.ReadCommitted;
                case System.Transactions.IsolationLevel.ReadUncommitted:
                    return IsolationLevel.ReadUncommitted;
                case System.Transactions.IsolationLevel.Snapshot:
                    return IsolationLevel.Snapshot;
                case System.Transactions.IsolationLevel.Chaos:
                    return IsolationLevel.Chaos;
                case System.Transactions.IsolationLevel.Unspecified:
                    return IsolationLevel.Unspecified;
            }

            return IsolationLevel.ReadCommitted;
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


        const string SqlPurge = @"DELETE FROM [{0}]";

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerPollingDequeueStrategy));

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        CountdownEvent countdownEvent;
        Action<TransportMessage, Exception> endProcessMessage;
        [SkipWeaving]
        readonly PipelineExecutor pipelineExecutor;

        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;
        SecondaryReceiveSettings secondaryReceiveSettings;
        bool purgeOnStartup;
        Address primaryAddress;
        TableBasedQueue errorQueue;
        TransactionSettings settings;
        CancellationTokenSource tokenSource;
        TransactionOptions transactionOptions;
        Func<TransportMessage, bool> tryProcessMessage;

        class ReceiveResult
        {
            public Exception Exception { get; set; }
            public TransportMessage Message { get; set; }
        }
    }
}