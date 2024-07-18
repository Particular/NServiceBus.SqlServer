namespace NServiceBus.Transport.Sql.Shared.DelayedDelivery;

using System;
using System.Threading;
using System.Threading.Tasks;
using Logging;

public class BackOffStrategy(TimeProvider timeProvider = null)
{
    public void RegisterNewDueTime(DateTime dueTime)
    {
        NextDelayedMessage = dueTime;

        if (dueTime == DateTime.MinValue)
        {
            Logger.Debug("No delayed messages available...");
            DelayedMessageAvailable = false;
            return;
        }

        if (dueTime < NextExecutionTime)
        {
            Logger.Debug("New delayed message registered with dueTime earlier than previously known next dueTime");
            DelayedMessageAvailable = true;
            NextExecutionTime = dueTime;
            return;
        }

        Logger.Debug("Delayed messages found but dueTime is later than time to peek for new delayed messages.");
        DelayedMessageAvailable = true;
    }

    public async Task WaitForNextExecution(CancellationToken cancellationToken = new())
    {
        CalculateNextExecutionTime();

        // While running this loop, a new delayed message can be stored
        // and NextExecutionTime could be set to a new (sooner) time.
        while (timeProvider.GetUtcNow() < NextExecutionTime)
        {
            int waitTime = (int)(NextExecutionTime - timeProvider.GetUtcNow()).TotalMilliseconds;
            waitTime = waitTime < 1000 ? waitTime : 1000;

            if (waitTime > 0)
            {
                await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
            }
        }
        NextExecutionTime = DateTime.MaxValue;
    }

    void CalculateNextExecutionTime()
    {
        IncreaseExponentialBackOff();

        var calculatedBackoffTime = timeProvider.GetUtcNow().AddMilliseconds(milliseconds);

        // If the next delayed message is coming up before the back-off time, use that.
        if (DelayedMessageAvailable && calculatedBackoffTime > NextDelayedMessage)
        {
            Logger.Debug(
                $"Scheduling next attempt to move matured delayed messages for time of next message due at {NextDelayedMessage}.");
            NextExecutionTime = NextDelayedMessage;
            // We find a better time to execute, so we can reset the exponential back off
            milliseconds = InitialBackOffTime;
            return;
        }

        Logger.Debug(
            $"Exponentially backing off for {milliseconds / 1000} seconds until {calculatedBackoffTime.UtcDateTime}.");
        NextExecutionTime = calculatedBackoffTime.UtcDateTime;
    }

    void IncreaseExponentialBackOff()
    {
        milliseconds *= 2;
        if (milliseconds > MaximumDelayUntilNextPeek)
        {
            milliseconds = MaximumDelayUntilNextPeek;
        }
    }

    const int MaximumDelayUntilNextPeek = 60000;
    const int InitialBackOffTime = 500;

    internal DateTime NextDelayedMessage { get; set; } = timeProvider.GetUtcNow().UtcDateTime;
    internal DateTime NextExecutionTime { get; set; } = timeProvider.GetUtcNow().AddSeconds(2).UtcDateTime;
    bool DelayedMessageAvailable { get; set; }

    int milliseconds = InitialBackOffTime; // First time multiplied will be 1 second.
    static readonly ILog Logger = LogManager.GetLogger<BackOffStrategy>();
    readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
}