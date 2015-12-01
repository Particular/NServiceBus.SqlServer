#pragma warning disable 1591

//TODO: add meaningful messages 

namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Transports.SQLServer.ConnectionStrings;

    public static partial class SqlServerTransportSettingsExtensions
    {
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "")]
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            IEnumerable<EndpointConnectionInfo> connectionInformationCollection)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "")]
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            params EndpointConnectionInfo[] connectionInformationCollection)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "")]
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            Func<string, ConnectionInfo> connectionInformationProvider)
        {
            throw new NotImplementedException();
        }
    }
}

namespace NServiceBus.Transports.SQLServer.ConnectionStrings
{
    using System;

    public class ConnectionInfo
    {
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "")]
        public static ConnectionInfo Create()
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "")]
        public ConnectionInfo UseSchema(string schemaName)
        {
            throw new NotImplementedException();
        }
    }

    public class EndpointConnectionInfo
    {
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "")]
        public static EndpointConnectionInfo For(string endpoint)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "3.0", Message = "")]
        public EndpointConnectionInfo UseSchema(string schemaName)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591
