namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;

    public class When_sending_raw_transport_messages : NServiceBusAcceptanceTest
    {
        static string SenderEndpoint => Conventions.EndpointNamingConvention(typeof(Sender));
        static string SatelliteAddress => SenderEndpoint + ".Satellite";

        [Test]
        public void Should_not_modify_reply_to_header()
        {
            var context = new Context
            {
                Id = Guid.NewGuid()
            };

            Scenario.Define(context)
                    .WithEndpoint<Sender>(b => b.Given((bus, ctx) =>
                    {
                        var sender = ((UnicastBus) bus).Builder.Build<ISendMessages>();
                        var headers = new Dictionary<string, string>
                        {
                            [Headers.ReplyToAddress] = "ReplyHere@SomeSchema"
                        };
                        var message = new TransportMessage(Guid.NewGuid().ToString(), headers);
                        sender.Send(message, new SendOptions(SatelliteAddress));
                    }))
                    .Done(c => c.WasCalled)
                    .Run();

            Assert.True(context.WasCalled, "The message handler should be called");
            Assert.AreEqual("ReplyHere@SomeSchema", context.ReplyToAddress);
        }


        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
            public string ReplyToAddress { get; set; }
            public Guid Id { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class CheckSatellite : ISatellite
        {
            public Context Context { get; set; }

            public bool Handle(TransportMessage message)
            {
                Context.ReplyToAddress = message.Headers[Headers.ReplyToAddress];
                Context.WasCalled = true;
                return true;
            }

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public Address InputAddress => Address.Parse(SatelliteAddress);
            public bool Disabled => false;
        }
    }
}