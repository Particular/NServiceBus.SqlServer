
namespace WireCompatibilityTests.TestBehaviors.V8
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using TestComms;
    using TestLogicApi;

    class Sender : ITestBehavior
    {
        public EndpointConfiguration Configure(Dictionary<string, string> args)
        {
            var config = new EndpointConfiguration("Sender");

            var transportDefinition = new LearningTransport();

            var routing = config.UseTransport(transportDefinition);
            routing.RouteToEndpoint(typeof(MyRequest), "Receiver");

            return config;
        }

#pragma warning disable PS0018
        public async Task Execute(IEndpointInstance endpointInstance)
#pragma warning restore PS0018
        {
            await endpointInstance.Send(new MyRequest()).ConfigureAwait(false);
        }

        public class MyResponseHandler : IHandleMessages<MyResponse>
        {
            ITestContextAccessor contextAccessor;

            public MyResponseHandler(ITestContextAccessor contextAccessor)
            {
                this.contextAccessor = contextAccessor;
            }

            public Task Handle(MyResponse message, IMessageHandlerContext context)
            {
                contextAccessor.SetFlag("ResponseReceived", true);
                contextAccessor.Success();

                return Task.CompletedTask;
            }
        }
    }
}