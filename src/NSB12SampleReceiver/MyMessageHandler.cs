using NSB12SampleMessages;
using NServiceBus;
using System;
using System.Threading.Tasks;
using Topics.Radical;

namespace NSB12SampleReceiver
{
    class MyMessageHandler : IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, IMessageHandlerContext context)
        {
            throw new ArgumentException("Poison msg");
            //using (ConsoleColor.Cyan.AsForegroundColor())
            //{
            //    Console.WriteLine("Sending MyReply to:  {0}", context.ReplyToAddress);

            //    var reply = new MyReply()
            //    {
            //        Content = "How you doing?"
            //    };

            //    context.ReplyAsync(reply);

            //    Console.WriteLine("Reply sent.");
            //}

            //return Task.FromResult(0);
        }
    }
}
