#pragma warning disable 1591

namespace NServiceBus.Transport.SqlServer
{
    using System;

    public partial class DelayedDeliverySettings
    {
        [ObsoleteEx(RemoveInVersion = "7.0", TreatAsErrorFromVersion = "6.0", Message = "Compatibility with the timeout manager is now disabled by default.")]
        public void DisableTimeoutManagerCompatibility()
        {
            throw new NotImplementedException();
        }
    }

}

#pragma warning restore 1591