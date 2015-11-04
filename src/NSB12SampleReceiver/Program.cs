using NServiceBus;
using System;

namespace NSB12SampleReceiver
{
    using NSB12SampleMessages;
    using NServiceBus.Features;
    using NServiceBus.Transports.SQLServer.Light;

    class Program
    {
        static void Main(string[] args)
        {
            var cfg = new BusConfiguration();

            cfg.UsePersistence<InMemoryPersistence>();
            cfg.UseSerialization<JsonSerializer>();
            cfg.UseTransport<SqlServerTransport>();
            cfg.DisableFeature<Audit>();
            cfg.Transactions().DisableDistributedTransactions();
            cfg.Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith("Messages"));

            var stats = new Statistics("Read", 1000);

            stats.Start();

            cfg.RegisterComponents(c => c.RegisterSingleton(stats));

            using (var bus = Bus.Create(cfg).StartAsync().Result)
            {
                Console.Read();
            }
        }
    }
}