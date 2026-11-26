namespace NServiceBus.Transport.SqlServer.UnitTests;

using System;
using NUnit.Framework;

[NonParallelizable]
public class AppContextSwitchesTests
{
    [TearDown]
    public void ResetSwitches()
    {
        AppContext.SetSwitch(AppContextSwitches.UseIndependentDelayedMessageMoversSwitchName, false);
        AppContext.SetSwitch(AppContextSwitches.DisableReceiveAnchoringSwitchName, false);
    }

    [Test]
    public void Independent_movers_are_off_by_default() =>
        Assert.That(AppContextSwitches.UseIndependentDelayedMessageMovers, Is.False);

    [Test]
    public void Independent_movers_follow_the_switch()
    {
        AppContext.SetSwitch("NServiceBus.Transport.SqlServer.DelayedDelivery.UseIndependentMovers", true);

        Assert.That(AppContextSwitches.UseIndependentDelayedMessageMovers, Is.True);
    }

    [Test]
    public void Receive_anchoring_is_on_by_default() =>
        Assert.That(AppContextSwitches.DisableReceiveAnchoring, Is.False);

    [Test]
    public void Receive_anchoring_follows_the_switch()
    {
        AppContext.SetSwitch("NServiceBus.Transport.SqlServer.Receiving.DisableAnchoring", true);

        Assert.That(AppContextSwitches.DisableReceiveAnchoring, Is.True);
    }
}
