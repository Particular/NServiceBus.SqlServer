﻿using System.Threading;
using System.Threading.Tasks;
using NServiceBus;

class Publisher : Base
{
    public override async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        await base.Execute(endpointInstance, cancellationToken).ConfigureAwait(false);
        await endpointInstance.Publish(new MyEvent(), cancellationToken).ConfigureAwait(false);
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
