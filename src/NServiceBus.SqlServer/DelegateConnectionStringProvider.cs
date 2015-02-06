namespace NServiceBus.Transports.SQLServer
{
    using System;

    class DelegateConnectionStringProvider : IConnectionStringProvider
    {
        readonly Func<string, ConnectionInfo> connectionStringProvider;
        readonly LocalConnectionParams localConnectionParams;

        public DelegateConnectionStringProvider(Func<string, ConnectionInfo> connectionStringProvider, LocalConnectionParams localConnectionParams)
        {
            this.connectionStringProvider = connectionStringProvider;
            this.localConnectionParams = localConnectionParams;
        }

        public ConnectionParams GetForDestination(Address destination)
        {
            var connectionInfo = connectionStringProvider(destination.Queue);
            return connectionInfo != null 
                ? connectionInfo.CreateConnectionParams(localConnectionParams) 
                : null;
        }
    }
}