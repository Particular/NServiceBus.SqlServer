﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Customization;

class Base
{
    string endpointName;

    protected Base(string endpointName)
    {
        this.endpointName = endpointName;
    }

    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var config = new EndpointConfiguration(endpointName);
        config.EnableInstallers();
        config.UsePersistence<InMemoryPersistence>();

        var transport = config.UseTransport<SqlServerTransport>();
        transport.ConnectionString(opts.ConnectionString);
        transport.Transactions(TransportTransactionMode.ReceiveOnly);

        config.Conventions().DefiningMessagesAs(t => t.GetInterfaces().Any(x => x.Name == "IMessage"));
        config.Conventions().DefiningCommandsAs(t => t.GetInterfaces().Any(x => x.Name == "ICommand"));
        config.Conventions().DefiningEventsAs(t => t.GetInterfaces().Any(x => x.Name == "IEvent"));

        config.AuditProcessedMessagesTo(opts.AuditQueue);
        config.AddHeaderToAllOutgoingMessages(nameof(opts.TestRunId), opts.TestRunId);
        config.Pipeline.Register(new DiscardBehavior(opts.TestRunId), nameof(DiscardBehavior));

        Configure(opts, config, transport, transport.Routing());

        return config;
    }

    protected virtual void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
    )
    {
    }

    public virtual Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}