namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;

    class CompositeConnectionStringProvider : IConnectionStringProvider
    {
        readonly IEnumerable<IConnectionStringProvider> components;

        public CompositeConnectionStringProvider(params IConnectionStringProvider[] components)
        {
            this.components = components;
        }

        public ConnectionParams GetForDestination(Address destination)
        {
            return components.Select(x => x.GetForDestination(destination)).First(x => x != null);
        }
    }
}