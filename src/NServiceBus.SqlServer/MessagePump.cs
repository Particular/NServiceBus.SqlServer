using System;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQLServer
{
    class MessagePump : IPushMessages
    {
        public void Init(Func<PushContext, Task> pipe, PushSettings settings)
        {
        }

        public void Start(PushRuntimeSettings limitations)
        {
        }

        public Task Stop()
        {
            return Task.FromResult(0);
        }
    }
}