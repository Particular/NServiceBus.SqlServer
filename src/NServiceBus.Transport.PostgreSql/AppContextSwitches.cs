namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Diagnostics.CodeAnalysis;
using Particular.Obsoletes;

static class AppContextSwitches
{
    [PreObsolete("https://github.com/Particular/NServiceBus.SqlServer/issues/1831",
        Note = "The independent movers fallback and this switch will be removed in the next major version.")]
    public const string UseIndependentDelayedMessageMoversSwitchName = "NServiceBus.Transport.PostgreSql.DelayedDelivery.UseIndependentMovers";

    [FeatureSwitchDefinition(UseIndependentDelayedMessageMoversSwitchName)]
    [PreObsolete("https://github.com/Particular/NServiceBus.SqlServer/issues/1831",
        Note = "The independent movers fallback and this switch will be removed in the next major version.")]
    public static bool UseIndependentDelayedMessageMovers =>
        AppContext.TryGetSwitch(UseIndependentDelayedMessageMoversSwitchName, out var isEnabled) && isEnabled;
}
