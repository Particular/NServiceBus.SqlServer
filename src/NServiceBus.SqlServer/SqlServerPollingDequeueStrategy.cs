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
    using Serializers.Json;
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
        /// The name of the schema to use or null to use user's default schema.
        /// </summary>
        public string SchemaName { get; set; }

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

            if (purgeOnStartup)
            {
                PurgeTable(AllTables());
            }
        }

        IEnumerable<string> AllTables()
        {
            yield return primaryAddress.Queue.GetTableName(SchemaName);
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
                StartReceiveThread(primaryAddress.Queue.GetTableName(SchemaName));
            }
            for (var i = 0; i < SecondaryReceiveSettings.MaximumConcurrencyLevel; i++)
            {
                StartReceiveThread(SecondaryReceiveSettings.ReceiveQueue.GetTableName(SchemaName));
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
                        if (settings.IsTransactional)
                        {
                            if (settings.SuppressDistributedTransactions)
                            {
                                result = TryReceiveWithNativeTransaction(query);
                            }
                            else
                            {
                                result = TryReceiveWithTransactionScope(query);
                            }
                        }
                        else
                        {
                            result = TryReceiveWithNoTransaction(query);
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

        ReceiveResult TryReceiveWithNoTransaction(string sql)
        {
            var result = new ReceiveResult();

            var message = Receive(sql);

            if (message == null)
            {
                return result;
            }

            result.Message = message;
            try
            {
                tryProcessMessage(message);
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }

            return result;
        }

        ReceiveResult TryReceiveWithTransactionScope(string sql)
        {
            var result = new ReceiveResult();

            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    try
                    {
                        pipelineExecutor.CurrentContext.Set(string.Format("SqlConnection-{0}", ConnectionString), connection);

                        connection.Open();

                        TransportMessage message;

                        using (var command = new SqlCommand(sql, connection)
                        {
                            CommandType = CommandType.Text
                        })
                        {
                            message = ExecuteReader(command);
                        }

                        if (message == null)
                        {
                            scope.Complete();
                            return result;
                        }

                        result.Message = message;

                        try
                        {
                            if (tryProcessMessage(message))
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

        ReceiveResult TryReceiveWithNativeTransaction(string sql)
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

                            TransportMessage message;
                            try
                            {
                                message = ReceiveWithNativeTransaction(sql, connection, transaction);
                            }
                            catch (Exception)
                            {
                                transaction.Rollback();
                                throw;
                            }

                            if (message == null)
                            {
                                transaction.Commit();
                                return result;
                            }

                            result.Message = message;

                            try
                            {
                                if (tryProcessMessage(message))
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

        TransportMessage Receive(string sql)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(sql, connection)
                {
                    CommandType = CommandType.Text
                })
                {
                    return ExecuteReader(command);
                }
            }
        }

        TransportMessage ReceiveWithNativeTransaction(string sql, SqlConnection connection, SqlTransaction transaction)
        {
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                return ExecuteReader(command);
            }
        }

        TransportMessage ExecuteReader(SqlCommand command)
        {
            using (var dataReader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (dataReader.Read())
                {
                    var id = dataReader.GetGuid(0).ToString();

                    DateTime? expireDateTime = null;
                    if (!dataReader.IsDBNull(4))
                    {
                        expireDateTime = dataReader.GetDateTime(4);
                    }

                    //Has message expired?
                    if (expireDateTime.HasValue && expireDateTime.Value < DateTime.UtcNow)
                    {
                        Logger.InfoFormat("Message with ID={0} has expired. Removing it from queue.", id);
                        return null;
                    }

                    var headers = (Dictionary<string, string>)Serializer.DeserializeObject(dataReader.GetString(5), typeof(Dictionary<string, string>));
                    var correlationId = dataReader.IsDBNull(1) ? null : dataReader.GetString(1);
                    var recoverable = dataReader.GetBoolean(3);
                    var body = dataReader.IsDBNull(6) ? null : dataReader.GetSqlBinary(6).Value;

                    var message = new TransportMessage(id, headers)
                    {
                        CorrelationId = correlationId,
                        Recoverable = recoverable,
                        Body = body ?? new byte[0]
                    };

                    var replyToAddress = dataReader.IsDBNull(2) ? null : dataReader.GetString(2);

                    if (!string.IsNullOrEmpty(replyToAddress))
                    {
                        message.Headers[Headers.ReplyToAddress] = replyToAddress;
                    }

                    if (expireDateTime.HasValue)
                    {
                        message.TimeToBeReceived = TimeSpan.FromTicks(expireDateTime.Value.Ticks - DateTime.UtcNow.Ticks);
                    }

                    return message;
                }
            }

            return null;
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


        const string SqlReceive =
            @"WITH message AS (SELECT TOP(1) * FROM {0} WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowVersion] ASC) 
			DELETE FROM message 
			OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, 
			deleted.Recoverable, deleted.Expires, deleted.Headers, deleted.Body;";

        const string SqlPurge = @"DELETE FROM {0}";

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerPollingDequeueStrategy));
        readonly JsonMessageSerializer Serializer = new JsonMessageSerializer(null);

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        CountdownEvent countdownEvent;
        Action<TransportMessage, Exception> endProcessMessage;
        [SkipWeaving]
        readonly PipelineExecutor pipelineExecutor;

        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;
        SecondaryReceiveSettings secondaryReceiveSettings;
        bool purgeOnStartup;
        Address primaryAddress;
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