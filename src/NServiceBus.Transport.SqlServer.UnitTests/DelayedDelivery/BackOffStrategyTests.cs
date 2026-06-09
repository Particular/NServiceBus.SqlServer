namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class BackOffStrategyTests
{
    [Test]
    public async Task When_NewDueTime_Is_Earlier_Then_LastKnownDueTime_Should_Use_NewDueTime()
    {
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        DateTime expectedNextDelayedMessage = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(1);
        strategy.RegisterNewDueTime(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(5));
        strategy.RegisterNewDueTime(expectedNextDelayedMessage);

        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false);

        using (Assert.EnterMultipleScope())
        {
            // We ignore calculating the time to wait, because that's not interesting in this test.
            Assert.That(strategy.NextDelayedMessage, Is.EqualTo(expectedNextDelayedMessage));
            Assert.That(strategy.NextExecutionTime, Is.EqualTo(DateTime.MaxValue));
        }
    }

    [Test]
    public async Task When_NewDueTime_Is_Later_Then_LastKnownDueTime_Should_Ignore_It_For_Now()
    {
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        DateTime expectedNextDelayedMessage = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(10);
        strategy.RegisterNewDueTime(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(5));
        strategy.RegisterNewDueTime(expectedNextDelayedMessage);

        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false);

        using (Assert.EnterMultipleScope())
        {
            // We ignore calculating the time to wait, because that's not interesting in this test.
            Assert.That(strategy.NextDelayedMessage, Is.EqualTo(expectedNextDelayedMessage));
            Assert.That(strategy.NextExecutionTime, Is.EqualTo(DateTime.MaxValue));
        }
    }

    [Test]
    public async Task When_No_DelayedMessages_Available_Should_Backoff_Exponentially()
    {
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = timeProvider.GetUtcNow().UtcDateTime;
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 1 second
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 2 more seconds
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 4 more seconds
        var afterWaiting = timeProvider.GetUtcNow().UtcDateTime;

        Assert.That(RoundOff(beforeWaiting, afterWaiting), Is.EqualTo(7));
    }

    [Test]
    public async Task When_NextDelayedMessage_Is_Sooner_Than_ExponentialBackoff_Should_Use_NextDelayeMessageDueTime()
    {
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = timeProvider.GetUtcNow().UtcDateTime;
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 1 second
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 2 more seconds
        strategy.RegisterNewDueTime(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(1)); // waits 1 more second
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // does NOT wait 4 more seconds
        var afterWaiting = timeProvider.GetUtcNow().UtcDateTime;

        Assert.That(RoundOff(beforeWaiting, afterWaiting), Is.EqualTo(4));
    }

    [Test]
    public async Task When_NextDelayedMessage_Is_Later_Than_ExponentialBackoff_Should_Use_ExponentialBackoffTime()
    {
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = timeProvider.GetUtcNow().UtcDateTime;
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 1 second
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 2 more seconds
        strategy.RegisterNewDueTime(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(10));
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 4 more seconds
        var afterWaiting = timeProvider.GetUtcNow().UtcDateTime;

        Assert.That(RoundOff(beforeWaiting, afterWaiting), Is.EqualTo(7));
    }

    [Test]
    public async Task When_NextDelayedMessage_Is_Up_Than_ExponentialBackoff_Should_Use_NextDelayeMessageDueTime()
    {
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = timeProvider.GetUtcNow().UtcDateTime;
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 1 second
        strategy.RegisterNewDueTime(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(4));
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false); // waits 2 more seconds
        // Following line waits 2 more seconds because of next delayed message is up.
        await WaitForNextExecution(strategy, timeProvider, CancellationToken.None).ConfigureAwait(false);
        var afterWaiting = timeProvider.GetUtcNow().UtcDateTime;

        Assert.That(RoundOff(beforeWaiting, afterWaiting), Is.EqualTo(5));
    }

    [Test]
    public void When_AfterWaiting_Takes_Little_Over_A_Second_Should_Still_Count_As_Second()
    {
        var now = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);
        var x = RoundOff(now, now.AddMilliseconds(999));
        var y = RoundOff(now, now.AddMilliseconds(1001));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(x, Is.Zero);
            Assert.That(y, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task When_WaitForNextExecution_Waits_It_Should_Wait_For_Exact_Difference_Between_Now_And_Next_Due_Time()
    {
        // Arrange
        var oneMillisecond = TimeSpan.FromMilliseconds(1);
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        strategy.RegisterNewDueTime(timeProvider.GetUtcNow().Add(oneMillisecond).UtcDateTime);
        var timerVersion = timeProvider.TimerVersion;

        var waitTask = strategy.WaitForNextExecution();
        var timer = await timeProvider.WaitForTimerCreatedAfter(timerVersion, CancellationToken.None).ConfigureAwait(false);
        timeProvider.Advance(timer.DueTime);
        await waitTask.ConfigureAwait(false);

        Assert.That(timer.DueTime, Is.EqualTo(oneMillisecond));
    }

    [Test]
    public void When_Multiple_DueTimes_Are_Registered_Concurrently_Should_Use_Earliest_Execution_Time()
    {
        var timeProvider = new FakeTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var earliestDueTime = now.AddMilliseconds(1);
        var dueTimes = Enumerable.Range(1, 1000)
            .Select(offset => now.AddMilliseconds(offset))
            .Reverse()
            .ToArray();

        Parallel.ForEach(dueTimes, strategy.RegisterNewDueTime);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.NextExecutionTime, Is.EqualTo(earliestDueTime));
            Assert.That(strategy.NextDelayedMessage, Is.Not.EqualTo(DateTime.MinValue));
        }
    }

    [Test]
    public async Task When_DueTime_Is_Registered_While_Waiting_Should_Complete_After_Next_WakeUp()
    {
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var timerVersion = timeProvider.TimerVersion;
        var waitTask = strategy.WaitForNextExecution();
        var timer = await timeProvider.WaitForTimerCreatedAfter(timerVersion, CancellationToken.None).ConfigureAwait(false);
        var dueTime = timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(100);
        strategy.RegisterNewDueTime(dueTime);

        Assert.That(waitTask.IsCompleted, Is.False);

        timeProvider.Advance(timer.DueTime);
        await waitTask.ConfigureAwait(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.NextDelayedMessage, Is.EqualTo(dueTime));
            Assert.That(strategy.NextExecutionTime, Is.EqualTo(DateTime.MaxValue));
        }
    }

    static async Task WaitForNextExecution(BackOffStrategy strategy, CaptureWhenTimerDueTimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var timerVersion = timeProvider.TimerVersion;
        var waitTask = strategy.WaitForNextExecution(cancellationToken);

        while (!waitTask.IsCompleted)
        {
            var timerCreated = timeProvider.WaitForTimerCreatedAfter(timerVersion, cancellationToken);
            var completedTask = await Task.WhenAny(waitTask, timerCreated).ConfigureAwait(false);
            if (completedTask == waitTask)
            {
                break;
            }

            var timer = await timerCreated.ConfigureAwait(false);
            timerVersion = timer.Version;
            timeProvider.Advance(timer.DueTime);
        }

        await waitTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Prevent flaky tests by allowing 999ms offset
    /// </summary>
    int RoundOff(DateTime before, DateTime after)
    {
        var difference = (after - before).TotalSeconds;
        return (int)double.Round(difference, MidpointRounding.ToZero);
    }

    class CaptureWhenTimerDueTimeProvider : FakeTimeProvider
    {
        public int TimerVersion
        {
            get
            {
                lock (syncRoot)
                {
                    return timerVersion;
                }
            }
        }

        public Task<TimerInfo> WaitForTimerCreatedAfter(int observedTimerVersion, CancellationToken cancellationToken = default)
        {
            lock (syncRoot)
            {
                if (timerVersion > observedTimerVersion)
                {
                    return Task.FromResult(new TimerInfo(timerVersion, dueTime));
                }

                return timerCreated.Task.WaitAsync(cancellationToken);
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            TaskCompletionSource<TimerInfo> created;
            TimerInfo timerInfo;

            lock (syncRoot)
            {
                this.dueTime = dueTime;
                timerVersion++;
                timerInfo = new TimerInfo(timerVersion, dueTime);
                created = timerCreated;
                timerCreated = CreateTimerCreatedTask();
            }

            _ = created.TrySetResult(timerInfo);
            return base.CreateTimer(callback, state, dueTime, period);
        }

        static TaskCompletionSource<TimerInfo> CreateTimerCreatedTask() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        readonly Lock syncRoot = new();
        TaskCompletionSource<TimerInfo> timerCreated = CreateTimerCreatedTask();
        TimeSpan dueTime;
        int timerVersion;

        public sealed record TimerInfo(int Version, TimeSpan DueTime);
    }
}
