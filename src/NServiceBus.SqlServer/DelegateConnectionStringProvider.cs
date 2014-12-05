namespace NServiceBus.Transports.SQLServer
{
    using System;

    class DelegateConnectionStringProvider : IConnectionStringProvider
    {
        readonly Func<string, ConnectionInfo> connectionStringProvider;
        readonly ConnectionParams defaultConnectionParams;

        public DelegateConnectionStringProvider(Func<string, ConnectionInfo> connectionStringProvider, ConnectionParams defaultConnectionParams)
        {
            this.connectionStringProvider = connectionStringProvider;
            this.defaultConnectionParams = defaultConnectionParams;
        }

        public ConnectionParams GetForDestination(Address destination)
        {
            var connectionInfo = connectionStringProvider(destination.Queue);
            return connectionInfo != null 
                ? connectionInfo.CreateConnectionParams(defaultConnectionParams) 
                : null;
        }
    }
}