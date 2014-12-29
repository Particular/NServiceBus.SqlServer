namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;

    class CollectionConnectionStringProvider : IConnectionStringProvider
    {
        readonly ConnectionParams defaultConnectionParams;
        readonly IEnumerable<EndpointConnectionInfo> connectionStrings;

        public CollectionConnectionStringProvider(IEnumerable<EndpointConnectionInfo> connectionStrings, ConnectionParams defaultConnectionParams)
        {
            this.defaultConnectionParams = defaultConnectionParams;
            this.connectionStrings = connectionStrings.ToList();
        }

        public ConnectionParams GetForDestination(Address destination)
        {
            var found = connectionStrings.FirstOrDefault(x => x.Endpoint == destination.Queue);
            return found != null
                ? found.CreateConnectionParams(defaultConnectionParams)
                : null;
        }
    }
}