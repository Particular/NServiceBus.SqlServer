namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IMulticastToUnicastConverter
    {
        Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation);
    }
}