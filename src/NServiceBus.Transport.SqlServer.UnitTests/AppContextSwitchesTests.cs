namespace NServiceBus.Transport.SqlServer.UnitTests;

using System;
using NUnit.Framework;

[NonParallelizable]
public class AppContextSwitchesTests
{
    [TearDown]
    public void ResetSwitch() => AppContext.SetSwitch(AppContextSwitches.UseIndependentDelayedMessageMoversSwitchName, false);

    [Test]
    public void Independent_movers_are_off_by_default() =>
        Assert.That(AppContextSwitches.UseIndependentDelayedMessageMovers, Is.False);

    [Test]
    public void Independent_movers_follow_the_switch()
    {
        AppContext.SetSwitch("NServiceBus.Transport.SqlServer.DelayedDelivery.UseIndependentMovers", true);

        Assert.That(AppContextSwitches.UseIndependentDelayedMessageMovers, Is.True);
    }
}
