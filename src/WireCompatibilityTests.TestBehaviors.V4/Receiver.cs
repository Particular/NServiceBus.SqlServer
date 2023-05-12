﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Receiver : ITestBehavior
{
    public Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args["ConnectionString"];

        var config = new EndpointConfiguration("Receiver");
        config.EnableInstallers();
        config.UsePersistence<InMemoryPersistence>();

        var transport = config.UseTransport<SqlServerTransport>();
        transport.Transactions(TransportTransactionMode.ReceiveOnly);
        transport.ConnectionString(connectionString);

        config.AuditProcessedMessagesTo("AuditSpy");

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