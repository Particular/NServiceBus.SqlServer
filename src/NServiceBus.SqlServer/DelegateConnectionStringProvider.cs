namespace NServiceBus.Transports.SQLServer
{
    using System;

    class DelegateConnectionStringProvider : IConnectionStringProvider
    {
        readonly Func<string, string> connectionStringProvider;

        public DelegateConnectionStringProvider(Func<string, string> connectionStringProvider)
        {
            this.connectionStringProvider = connectionStringProvider;
        }

        public string GetForDestination(Address destination)
        {
            return connectionStringProvider(destination.Queue);
        }
    }
}