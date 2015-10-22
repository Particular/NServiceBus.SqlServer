using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Extensibility;

namespace NServiceBus.Transports.SQLServer.Light
{
    class SqlServerMessageSender : IDispatchMessages
    {
        TableBasedQueue queue;

        public SqlServerMessageSender(TableBasedQueue queue)
        {
            this.queue = queue;
        }

        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ContextBag context)
        {
            foreach (var outgoingMessage in outgoingMessages)
            {
                queue.SendMessage(outgoingMessage);
            }

            return Task.FromResult(0);
        }
    }
}