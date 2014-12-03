namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;

    class CollectionConnectionStringProvider : IConnectionStringProvider
    {
        readonly IEnumerable<EndpointConnectionString> connectionStrings;

        public CollectionConnectionStringProvider(IEnumerable<EndpointConnectionString> connectionStrings)
        {
            this.connectionStrings = connectionStrings.ToList();
        }

        public string GetForDestination(Address destination)
        {
            var found = connectionStrings.FirstOrDefault(x => x.EndpointName == destination.Queue);
            return found != null
                ? found.ConnectionString
                : null;
        }
    }
}