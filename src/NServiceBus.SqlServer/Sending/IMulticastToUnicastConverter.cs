namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IMulticastToUnicastConverter
    {
        Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation);
    }
}