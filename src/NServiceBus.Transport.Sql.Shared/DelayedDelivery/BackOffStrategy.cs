namespace NServiceBus.Transport.Sql.Shared.DelayedDelivery;

using System;
using System.Threading;
using System.Threading.Tasks;
using Logging;

public class BackOffStrategy
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
        // Whoops, we should've already moved the delayed message! Quickly, move on! :-)
        if (AreDelayedMessagesMatured)
        {
            Logger.Debug(
                "Scheduling next attempt to move matured delayed messages immediately because a full batch was detected.");
            return;
        }

        CalculateNextExecutionTime();

        // While running this loop, a new delayed message can be stored
        // and NextExecutionTime could be set to a new (sooner) time.
        while (DateTime.UtcNow < NextExecutionTime)
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }

    public void CalculateNextExecutionTime()
    {
        IncreaseExponentialBackOff();

        var newExecutionTime = DateTime.UtcNow.AddMilliseconds(milliseconds);

        // If the next delayed message is coming up before the back-off time, use that.
        if (DelayedMessageAvailable && newExecutionTime > NextDelayedMessage)
        {
            Logger.Debug(
                $"Scheduling next attempt to move matured delayed messages for time of next message due at {NextDelayedMessage}.");
            NextExecutionTime = NextDelayedMessage;
            // We find a better time to execute, so we can reset the exponential back off
            milliseconds = 500;
            return;
        }

        Logger.Debug(
            $"Exponentially backing off for {milliseconds / 1000} seconds until {newExecutionTime}.");
        NextExecutionTime = newExecutionTime;
    }

    void IncreaseExponentialBackOff()
    {
        milliseconds *= 2;
        if (milliseconds > 60000)
        {
            milliseconds = 60000;
        }
    }

    bool AreDelayedMessagesMatured => DelayedMessageAvailable && NextExecutionTime < DateTime.UtcNow;

    DateTime NextDelayedMessage { get; set; } = DateTime.UtcNow;
    public DateTime NextExecutionTime { get; private set; } = DateTime.UtcNow.AddSeconds(2);
    bool DelayedMessageAvailable { get; set; }

    int milliseconds = 500; // First time multiplied will be 1 second.
    static readonly ILog Logger = LogManager.GetLogger<BackOffStrategy>();
}