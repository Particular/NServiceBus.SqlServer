using NSB12SampleMessages;
using NServiceBus;
using System;
using System.Threading.Tasks;
using Topics.Radical;

namespace NSB12SampleSender
{
    class MyReplyHandler : IHandleMessages<MyReply>
    {
        public IBus Bus { get; set; }

        public Task Handle(MyReply message, IMessageHandlerContext context)
        {
            using (ConsoleColor.Cyan.AsForegroundColor())
            {
                Console.WriteLine("Received MyReply from:  {0}", context.ReplyToAddress);
            }

            return Task.FromResult(0);
        }
    }
}
