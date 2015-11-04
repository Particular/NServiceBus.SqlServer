using NSB12SampleMessages;
using NServiceBus;
using System.Threading.Tasks;

namespace NSB12SampleReceiver
{
    class MyMessageHandler : IHandleMessages<MyMessage>
    {
        readonly Statistics stats;

        public MyMessageHandler(Statistics stats)
        {
            this.stats = stats;
        }

        public Task Handle(MyMessage message, IMessageHandlerContext context)
        {
            stats.MessageProcessed();

            return Task.FromResult(0);
        }
    }
}
