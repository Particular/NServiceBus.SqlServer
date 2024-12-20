namespace NServiceBus.Transport.Sql.Shared
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    interface IMulticastToUnicastConverter
    {
        Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation, CancellationToken cancellationToken = default);
    }
}