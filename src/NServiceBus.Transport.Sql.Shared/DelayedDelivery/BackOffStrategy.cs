namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Threading;
using System.Threading.Tasks;
using Logging;

class BackOffStrategy(TimeProvider timeProvider)
{
    public BackOffStrategy() : this(TimeProvider.System) { }

    public void RegisterNewDueTime(DateTime dueTime) =>
        UpdateState(static (current, dueTime) =>
        {
            if (dueTime == DateTime.MinValue)
            {
                Logger.Debug("No delayed messages available...");
                return current with
                {
                    NextDelayedMessage = dueTime,
                    DelayedMessageAvailable = false
                };
            }

            if (dueTime < current.NextExecutionTime)
            {
                Logger.Debug("New delayed message registered with dueTime earlier than previously known next dueTime");
                return current with
                {
                    NextDelayedMessage = dueTime,
                    NextExecutionTime = dueTime,
                    DelayedMessageAvailable = true
                };
            }

            Logger.Debug("Delayed messages found but dueTime is later than time to peek for new delayed messages.");
            return current with
            {
                NextDelayedMessage = dueTime,
                DelayedMessageAvailable = true
            };
        }, dueTime);

    public async Task WaitForNextExecution(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        UpdateState(static (current, now) => CalculateNextExecutionTime(current, now), now);

        // While running this loop, a new delayed message can be stored
        // and NextExecutionTime could be set to a new (sooner) time.
        while (true)
        {
            var current = Volatile.Read(ref state);
            var waitTime = current.NextExecutionTime - now;
            if (waitTime <= TimeSpan.Zero)
            {
                var nextState = current with
                {
                    NextExecutionTime = DateTime.MaxValue
                };

                if (Interlocked.CompareExchange(ref state, nextState, current) == current)
                {
                    return;
                }

                continue;
            }

            waitTime = waitTime < oneSecond ? waitTime : oneSecond;
            await Task.Delay(waitTime, timeProvider, cancellationToken).ConfigureAwait(false);

            now = timeProvider.GetUtcNow();
        }
    }

    static BackOffState CalculateNextExecutionTime(BackOffState current, DateTimeOffset now)
    {
        var milliseconds = IncreaseExponentialBackOff(current.Milliseconds);

        var calculatedBackoffTime = now.AddMilliseconds(milliseconds);

        // If the next delayed message is coming up before the back-off time, use that.
        if (current.DelayedMessageAvailable && calculatedBackoffTime > current.NextDelayedMessage)
        {
            Logger.Debug(
                $"Scheduling next attempt to move matured delayed messages for time of next message due at {current.NextDelayedMessage}.");
            // We find a better time to execute, so we can reset the exponential back off
            return current with
            {
                NextExecutionTime = current.NextDelayedMessage,
                Milliseconds = InitialBackOffTime
            };
        }

        Logger.Debug(
            $"Exponentially backing off for {milliseconds / 1000} seconds until {calculatedBackoffTime.UtcDateTime}.");
        return current with
        {
            NextExecutionTime = calculatedBackoffTime.UtcDateTime,
            Milliseconds = milliseconds
        };
    }

    static int IncreaseExponentialBackOff(int milliseconds)
    {
        milliseconds *= 2;
        if (milliseconds > MaximumDelayUntilNextPeek)
        {
            milliseconds = MaximumDelayUntilNextPeek;
        }

        return milliseconds;
    }

    void UpdateState<TArg>(Func<BackOffState, TArg, BackOffState> update, TArg arg)
    {
        while (true)
        {
            var current = Volatile.Read(ref state);
            var next = update(current, arg);
            if (Interlocked.CompareExchange(ref state, next, current) == current)
            {
                return;
            }
        }
    }

    const int MaximumDelayUntilNextPeek = 60000;
    const int InitialBackOffTime = 500;

    internal DateTime NextDelayedMessage => Volatile.Read(ref state).NextDelayedMessage;

    internal DateTime NextExecutionTime => Volatile.Read(ref state).NextExecutionTime;

    static readonly ILog Logger = LogManager.GetLogger<BackOffStrategy>();

    BackOffState state = new(
        timeProvider.GetUtcNow().UtcDateTime,
        timeProvider.GetUtcNow().UtcDateTime.AddSeconds(2),
        false,
        InitialBackOffTime);

    readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);

    sealed record BackOffState(
        DateTime NextDelayedMessage,
        DateTime NextExecutionTime,
        bool DelayedMessageAvailable,
        int Milliseconds);
}
