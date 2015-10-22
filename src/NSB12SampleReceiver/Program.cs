using NServiceBus;
using System;

namespace NSB12SampleReceiver
{
    using NServiceBus.Transports.SQLServer.Light;

    class Program
	{
		static void Main( string[] args )
		{
			var cfg = new BusConfiguration();

			cfg.UsePersistence<InMemoryPersistence>();
            cfg.UseSerialization<JsonSerializer>();
            cfg.UseTransport<SqlServer>();
			cfg.Conventions()
				.DefiningMessagesAs( t => t.Namespace != null && t.Namespace.EndsWith( "Messages" ) );

			using( var bus = Bus.Create( cfg ).StartAsync().Result )
			{
				Console.Read();
			}
		}
	}
}
