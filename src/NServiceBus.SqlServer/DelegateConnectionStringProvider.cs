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

        public ConnectionParams GetForDestination(string destination)
        {
            var connectionInfo = connectionStringProvider(destination);
            return connectionInfo != null 
                ? connectionInfo.CreateConnectionParams(localConnectionParams) 
                : null;
        }

        public bool AllowsNonLocalConnectionString { get { return true; }}
    }
}