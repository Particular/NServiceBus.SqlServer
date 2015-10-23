using NSB12SampleMessages;
using NServiceBus;
using System;
using System.Collections.Generic;

namespace NSB12SampleSender
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
            cfg.Transactions().Disable();
            cfg.Conventions()
				.DefiningCommandsAs( t => t.Namespace != null && t.Namespace.EndsWith( "Messages" ) );

			using(var bus = Bus.Create( cfg ).StartAsync().Result )
			{
				Logic.Run( setup =>
				{
					setup.DefineAction( ConsoleKey.S, "Sends a new message.", () =>
					{
						bus.SendAsync( new MyMessage()
						{
                            Content = "Test"
						} );
					} );
				} );
			}
		}
	}
}
