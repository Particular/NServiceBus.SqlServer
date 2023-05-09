﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class SchemaSender : Sender
{
    protected override void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig, TransportExtensions<SqlServerTransport> transportConfig)
    {
        base.Configure(args, endpointConfig, transportConfig);

        transportConfig.DefaultSchema("sender");
        transportConfig.UseSchemaForQueue("AuditSpy", "dbo");
        transportConfig.UseSchemaForEndpoint("Receiver", "receiver");
    }
}

class Sender : ITestBehavior
{
    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args["ConnectionString"];

        var config = new EndpointConfiguration("Sender");
        config.EnableInstallers();
        config.UsePersistence<InMemoryPersistence>();

        var transport = config.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.Transactions(TransportTransactionMode.ReceiveOnly);

        var routing = transport.Routing();
        routing.RouteToEndpoint(typeof(MyRequest), "Receiver");

        config.AuditProcessedMessagesTo("AuditSpy");

        Configure(args, config, transport);

        return config;
    }

    protected virtual void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig)
    {
    }

    public async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        await endpointInstance.Send(new MyRequest()).ConfigureAwait(false);
    }

    public class MyResponseHandler : IHandleMessages<MyResponse>
    {
#pragma warning disable PS0018
        public Task Handle(MyResponse message, IMessageHandlerContext context)
#pragma warning restore PS0018
        {
            return Task.CompletedTask;
        }
    }
}
