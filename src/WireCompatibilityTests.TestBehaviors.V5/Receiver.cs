﻿using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Receiver : ITestBehavior
{
    public Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var config = new EndpointConfiguration("Receiver");
        config.EnableInstallers();
        config.UsePersistence<InMemoryPersistence>();

        var transport = config.UseTransport<SqlServerTransport>();
        transport.Transactions(TransportTransactionMode.ReceiveOnly);
        transport.ConnectionString(opts.ConnectionString);

        config.AuditProcessedMessagesTo(opts.AuditQueue);

        return config;
    }

    public class MyRequestHandler : IHandleMessages<MyRequest>
    {
#pragma warning disable PS0018
        public Task Handle(MyRequest message, IMessageHandlerContext context)
#pragma warning restore PS0018
        {
            return context.Reply(new MyResponse());
        }
    }
}
