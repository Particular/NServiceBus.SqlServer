﻿
#pragma warning disable 1591

namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using ConnectionStrings;

    public static partial class SqlServerTransportSettingsExtensions
    {
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "Replaced by NServiceBus.Callbacks package")]
        public static TransportExtensions<SqlServerTransport> DisableCallbackReceiver(
            this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "Replaced by NServiceBus.Callbacks package")]
        public static TransportExtensions<SqlServerTransport> CallbackReceiverMaxConcurrency(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            int maxConcurrency)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify schema use `UseSpecificSchema()`, to specify connection string use `EnableLegacyMultiInstanceMode()`.")]
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            IEnumerable<EndpointConnectionInfo> connectionInformationCollection)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify connection string use `EnableLegacyMultiInstanceMode()`.")]
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            params EndpointConnectionInfo[] connectionInformationCollection)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify schema use `UseSpecificSchema()`, to specify connection string use `EnableLegacyMultiInstanceMode()`.")]
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            Func<string, ConnectionInfo> connectionInformationProvider)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "That config option is no longer supported.")]
        public static TransportExtensions<SqlServerTransport> PauseAfterReceiveFailure(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan delayTime)
        {
            throw new NotImplementedException();
        }
    }
}

namespace NServiceBus.Transport.SQLServer.ConnectionStrings
{
    using System;

    public class ConnectionInfo
    {
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify schema use `UseSpecificSchema()`, to specify connection string use `EnableLegacyMultiInstanceMode()`.")]
        public static ConnectionInfo Create()
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify schema use `UseSpecificSchema()`.")]
        public ConnectionInfo UseSchema(string schemaName)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify connection string use `EnableLegacyMultiInstanceMode()`.")]
        public ConnectionInfo UseConnectionString(string connectionString)
        {
            throw new NotImplementedException();
        }
    }

    public class EndpointConnectionInfo
    {
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify schema use `UseSpecificSchema()`, to specify connection string use `EnableLegacyMultiInstanceMode()`.")]
        public static EndpointConnectionInfo For(string endpoint)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0",
            Message = "Multi-instance setup is supported for backwards compatibility and won't be supported from Version 4.0. To specify schema use `UseSpecificSchema()`.")]
        public EndpointConnectionInfo UseSchema(string schemaName)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591