using NSB12SampleMessages;
using NServiceBus;
using System;

namespace NSB12SampleSender
{
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Features;
    using NServiceBus.Transports.SQLServer.Light;

    class Program
	{
		static void Main( string[] args )
		{
			var cfg = new BusConfiguration();

			cfg.UsePersistence<InMemoryPersistence>();
            cfg.UseSerialization<JsonSerializer>();
            cfg.UseTransport<SqlServerTransport>();
            cfg.DisableFeature<Audit>();
		    cfg.Transactions().DisableDistributedTransactions();
            cfg.Conventions().DefiningCommandsAs( t => t.Namespace != null && t.Namespace.EndsWith( "Messages" ) );

			using(var bus = Bus.Create(cfg).StartAsync().Result )
			{
                SpawnWriters(bus, 10, 1000);

			    Console.ReadKey();
			}
		}

        private static void SpawnWriters(IBus bus, int threads, int snapshotInterval)
        {
            var stats = new Statistics("Write", snapshotInterval);

            stats.Start();

            for (int i = 0; i < threads; i++)
            {
                new Thread(async () =>
                {
                    while (true)
                    {
                        await bus.SendAsync(new MyMessage());

                        stats.MessageProcessed();
                    }
                }).Start();
            }
        }
    }
}
