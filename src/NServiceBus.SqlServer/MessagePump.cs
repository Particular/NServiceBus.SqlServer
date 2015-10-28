using System;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Logging;
    using Light;

    class MessagePump : IPushMessages
    {
        //TODO: remove connection string and put that in the address (no mulit-db yet)
        public MessagePump(CriticalError criticalError, Func<TransactionSupport, ReceiveStrategy> receiveStrategyFactory, string connectionString)
        {
            this.connectionString = connectionString;
            this.receiveStrategyFactory = receiveStrategyFactory;
            this.criticalError = criticalError;
        }

        public void Init(Func<PushContext, Task> pipe, PushSettings settings)
        {
            this.pipeline = pipe;

            receiveStrategy = receiveStrategyFactory(settings.RequiredTransactionSupport);

            peekCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SqlPeek", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to peek " + settings.InputQueue, ex));
            receiveCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SqlReceive", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to receive from " + settings.InputQueue, ex));

            //TODO: queue addresses should have more structue cs+schema+table
            this.inputQueue = new TableBasedQueue(settings.InputQueue, "dbo", this.connectionString);
            this.errorQueue = new TableBasedQueue(settings.ErrorQueue, "dbo", this.connectionString);

            if (settings.PurgeOnStartup)
            {
                //TODO: do queue clean-up on startup
                //this.PurgeMessages();
            }
        }
        
        public void Start(PushRuntimeSettings limitations)
        {
            runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            cancellationToken = cancellationTokenSource.Token;

            messagePumpTask = Task.Factory.StartNew(() => ProcessMessages(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            // ReSharper disable once MethodSupportsCancellation
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var allTasks = runningReceiveTasks.Values.Concat(new[]
            {
                messagePumpTask
            });
            var finishedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
            runningReceiveTasks.Clear();
        }

        //TODO: check what this code is doing
        [DebuggerNonUserCode]
        async Task ProcessMessages()
        {
            try
            {
                await InnerProcessMessages().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // For graceful shutdown purposes
            }
            catch (Exception ex)
            {
                Logger.Error("Sql Message pump failed", ex);
                await peekCircuitBreaker.Failure(ex).ConfigureAwait(false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await ProcessMessages().ConfigureAwait(false);
            }
        }

        async Task InnerProcessMessages()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                string messageId;

                try
                {
                    //TODO: change that to peeking a collection of ids instead of one
                    if (inputQueue.TryPeek(out messageId) == false)
                    {
                        continue;
                    } 

                    peekCircuitBreaker.Success();
                }
                catch (Exception ex)
                {
                    Logger.Warn("Sql peek operation failed", ex);
                    await peekCircuitBreaker.Failure(ex).ConfigureAwait(false);
                    continue;
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await receiveStrategy.ReceiveMessage(messageId, inputQueue, errorQueue, pipeline)
                                             .ConfigureAwait(false);

                        receiveCircuitBreaker.Success();
                    }
                    //TODO: we need to make this fellow public in the core
                    //catch (MessageProcessingAbortedException)
                    //{
                        //expected to happen
                    //}
                    catch (Exception ex)
                    {
                        Logger.Warn("Sql receive operation failed", ex);
                        await receiveCircuitBreaker.Failure(ex).ConfigureAwait(false);
                    }
                    finally
                    {
                        concurrencyLimiter.Release();
                    }
                }, cancellationToken);

                await runningReceiveTasks.AddOrUpdate(task, task, (k, v) => task);

                // We insert the original task into the runningReceiveTasks because we want to await the completion
                // of the running receives. ExecuteSynchronously is a request to execute the continuation as part of
                // the transition of the antecedents completion phase. This means in most of the cases the continuation
                // will be executed during this transition and the antecedent task goes into the completion state only 
                // after the continuation is executed. This is not always the case. When the TPL thread handling the
                // antecedent task is aborted the continuation will be scheduled. But in this case we don't need to await
                // the continuation to complete because only really care about the receive operations. The final operation
                // when shutting down is a clear of the running tasks anyway.
                await task.ContinueWith(t =>
                {
                    Task toBeRemoved;
                    runningReceiveTasks.TryRemove(t, out toBeRemoved);
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;
        Func<PushContext, Task> pipeline;
        string connectionString;
        readonly Func<TransactionSupport, ReceiveStrategy> receiveStrategyFactory;
        readonly CriticalError criticalError;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource cancellationTokenSource;
        CancellationToken cancellationToken;
        RepeatedFailuresOverTimeCircuitBreaker peekCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker receiveCircuitBreaker;

        static ILog Logger = LogManager.GetLogger<MessagePump>();
        Task<Task> messagePumpTask;
        ReceiveStrategy receiveStrategy;
    }
}