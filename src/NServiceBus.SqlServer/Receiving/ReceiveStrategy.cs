namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Transports;

    abstract class ReceiveStrategy
    {
        public abstract Task<ReceiveStrategyResult> ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage);
    }

    abstract class ReceiveStrategy<T> : ReceiveStrategy
        where T : IReceiveStrategyContext
    {
        public sealed override async Task<ReceiveStrategyResult> ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var context = CreateContext(inputQueue))
            {
                await context.Initialize().ConfigureAwait(false);
                try
                {
                    var readResult = await context.TryReceive(inputQueue).ConfigureAwait(false);

                    if (readResult.IsPoison)
                    {
                        await DeadLetterPoisonMessage(errorQueue, context, readResult.PoisonMessage).ConfigureAwait(false);
                        context.Commit();
                        return ReceiveStrategyResult.PoisonMessage;
                    }

                    if (!readResult.Successful)
                    {
                        context.Commit();
                        return ReceiveStrategyResult.NoMessage;
                    }

                    var message = readResult.Message;

                    using (var pushCancellationTokenSource = new CancellationTokenSource())
                    using (var bodyStream = message.BodyStream)
                    {
                        var transportTransaction = context.TransportTransaction;

                        var bag = new ContextBag();
                        bag.Set(CreateDispatchStrategy(context));

                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, pushCancellationTokenSource, bag);
                        await onMessage(pushContext).ConfigureAwait(false);

                        if (pushCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            context.Rollback();
                            return ReceiveStrategyResult.ProcessingAborted;
                        }
                    }

                    context.Commit();
                    return ReceiveStrategyResult.Success;
                }
                catch (Exception)
                {
                    context.Rollback();
                    throw;
                }
            }
        }

        protected abstract T CreateContext(TableBasedQueue inputQueue);

        protected abstract IDispatchStrategy CreateDispatchStrategy(T context);

        protected virtual async Task DeadLetterPoisonMessage(TableBasedQueue errorQueue, IReceiveStrategyContext context, MessageRow poisonMessage)
        {
            await context.DeadLetter(errorQueue, poisonMessage).ConfigureAwait(false);
        }
    }
}