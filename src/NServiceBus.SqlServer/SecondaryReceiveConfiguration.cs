namespace NServiceBus.Features
{
    using System;
    using NServiceBus.Transports.SQLServer;

    class SecondaryReceiveConfiguration
    {
        public SecondaryReceiveConfiguration(Func<string, SecondaryReceiveSettings> getSecondaryReceiveSettings)
        {
            secondaryReceiveSettings = getSecondaryReceiveSettings;
        }

        public SecondaryReceiveSettings GetSettings(string queue)
        {
            return secondaryReceiveSettings(queue);
        }

        Func<string, SecondaryReceiveSettings> secondaryReceiveSettings;

    }
}