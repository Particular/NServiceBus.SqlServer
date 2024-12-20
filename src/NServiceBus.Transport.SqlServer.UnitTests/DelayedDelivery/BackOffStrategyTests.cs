namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using System;
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
        var strategy = new BackOffStrategy();
        DateTime expectedNextDelayedMessage = DateTime.UtcNow.AddSeconds(1);
        strategy.RegisterNewDueTime(DateTime.UtcNow.AddSeconds(5));
        strategy.RegisterNewDueTime(expectedNextDelayedMessage);

        await strategy.WaitForNextExecution().ConfigureAwait(false);

        // We ignore calculating the time to wait, because that's not interesting in this test.
        Assert.AreEqual(expectedNextDelayedMessage, strategy.NextDelayedMessage);
        Assert.AreEqual(DateTime.MaxValue, strategy.NextExecutionTime);
    }

    [Test]
    public async Task When_NewDueTime_Is_Later_Then_LastKnownDueTime_Should_Ignore_It_For_Now()
    {
        var strategy = new BackOffStrategy();
        DateTime expectedNextDelayedMessage = DateTime.UtcNow.AddSeconds(10);
        strategy.RegisterNewDueTime(DateTime.UtcNow.AddSeconds(5));
        strategy.RegisterNewDueTime(expectedNextDelayedMessage);

        await strategy.WaitForNextExecution().ConfigureAwait(false);

        // We ignore calculating the time to wait, because that's not interesting in this test.
        Assert.AreEqual(expectedNextDelayedMessage, strategy.NextDelayedMessage);
        Assert.AreEqual(DateTime.MaxValue, strategy.NextExecutionTime);
    }

    [Test]
    public async Task When_No_DelayedMessages_Available_Should_Backoff_Exponentially()
    {
        var strategy = new BackOffStrategy();
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = DateTime.UtcNow;
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 1 second
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 2 more seconds
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 4 more seconds
        var afterWaiting = DateTime.UtcNow;

        Assert.AreEqual(7, RoundOff(beforeWaiting, afterWaiting));
    }

    [Test]
    public async Task When_NextDelayedMessage_Is_Sooner_Than_ExponentialBackoff_Should_Use_NextDelayeMessageDueTime()
    {
        var strategy = new BackOffStrategy();
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = DateTime.UtcNow;
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 1 second
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 2 more seconds
        strategy.RegisterNewDueTime(DateTime.UtcNow.AddSeconds(1)); // waits 1 more second
        await strategy.WaitForNextExecution().ConfigureAwait(false); // does NOT wait 4 more seconds
        var afterWaiting = DateTime.UtcNow;

        Assert.AreEqual(4, RoundOff(beforeWaiting, afterWaiting));
    }

    [Test]
    public async Task When_NextDelayedMessage_Is_Later_Than_ExponentialBackoff_Should_Use_ExponentialBackoffTime()
    {
        var strategy = new BackOffStrategy();
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = DateTime.UtcNow;
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 1 second
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 2 more seconds
        strategy.RegisterNewDueTime(DateTime.UtcNow.AddSeconds(10));
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 4 more seconds
        var afterWaiting = DateTime.UtcNow;

        Assert.AreEqual(7, RoundOff(beforeWaiting, afterWaiting));
    }

    [Test]
    public async Task When_NextDelayedMessage_Is_Up_Than_ExponentialBackoff_Should_Use_NextDelayeMessageDueTime()
    {
        var strategy = new BackOffStrategy();
        strategy.RegisterNewDueTime(DateTime.MinValue);

        var beforeWaiting = DateTime.UtcNow;
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 1 second
        strategy.RegisterNewDueTime(DateTime.UtcNow.AddSeconds(4));
        await strategy.WaitForNextExecution().ConfigureAwait(false); // waits 2 more seconds
        // Following line waits 2 more seconds because of next delayed message is up.
        await strategy.WaitForNextExecution().ConfigureAwait(false);
        var afterWaiting = DateTime.UtcNow;

        Assert.AreEqual(5, RoundOff(beforeWaiting, afterWaiting));
    }

    [Test]
    public void When_AfterWaiting_Takes_Little_Over_A_Second_Should_Still_Count_As_Second()
    {
        var now = DateTime.UtcNow;
        var x = RoundOff(now, now.AddMilliseconds(999));
        var y = RoundOff(now, now.AddMilliseconds(1001));

        Assert.AreEqual(0, x);
        Assert.AreEqual(1, y);
    }

    [Test]
    public void When_WaitForNextExecution_Waits_It_Should_Wait_For_Exact_Difference_Between_Now_And_Next_Due_Time()
    {
        // Arrange
        var oneMillisecond = TimeSpan.FromMilliseconds(1);
        var timeProvider = new CaptureWhenTimerDueTimeProvider();
        var strategy = new BackOffStrategy(timeProvider);
        strategy.RegisterNewDueTime(timeProvider.GetUtcNow().Add(oneMillisecond).UtcDateTime);
        timeProvider.AutoAdvanceAmount = oneMillisecond;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1), TimeProvider.System);

        // Act
        async Task action() => await strategy.WaitForNextExecution(cts.Token).ConfigureAwait(false);

        // Assert
        Assert.ThrowsAsync<TaskCanceledException>(action);
        Assert.That(timeProvider.DueTime, Is.EqualTo(oneMillisecond));
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
        public TimeSpan DueTime { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            return base.CreateTimer(callback, state, dueTime, period);
        }
    }
}