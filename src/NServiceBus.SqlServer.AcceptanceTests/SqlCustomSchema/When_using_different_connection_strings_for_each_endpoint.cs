namespace NServiceBus.AcceptanceTests.SqlCustomSchema
{
    using System;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_using_different_connection_strings_for_each_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_use_configured_connection_string_when_replying()
        {
            var context = new Context()
            {
                Id = Guid.NewGuid()
            };

            Scenario.Define(context)
                   .WithEndpoint<Receiver>()
                   .WithEndpoint<Sender>(b => b.Given((bus, c) => bus.Send(new MyRequest
                   {
                       ContextId = c.Id
                   })))
                   .Done(c => context.GotResponse)
                   .Run();
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>( c => c.UseTransport<SqlServer>( ConnectionStringWith_sender_schema ) )
                    .AddMapping<MyRequest>(typeof(Receiver));
            }

            class MyReplyHandler : IHandleMessages<MyReply>
            {
                public IBus Bus { get; set; }
                public Context Context { get; set; }

                public void Handle(MyReply message)
                {
                    if (Context.Id != message.ContextId)
                    {
                        return;
                    }
                    Context.GotResponse = true;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>( c => c.UseTransport<SqlServer>( ConnectionStringWith_receiver_schema ) );
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                public IBus Bus { get; set; }
                public Context Context { get; set; }

                public void Handle(MyRequest message)
                {
                    if (Context.Id != message.ContextId)
                    {
                        return;
                    }
                    Bus.Reply(new MyReply
                    {
                        ContextId = message.ContextId
                    });
                }
            }
        }

        class MyRequest : IMessage
        {
            public Guid ContextId { get; set; }
        }

        class MyReply : IMessage
        {
            public Guid ContextId { get; set; }
        }

        class Context : ScenarioContext
        {
            public bool GotResponse { get; set; }
            public Guid Id { get; set; }
        }
    }
}