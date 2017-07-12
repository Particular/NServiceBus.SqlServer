namespace CompatibilityTests.Common
{
    using System;
    using Messages;

    public interface IEndpointFacade : IDisposable
    {
        IEndpointConfiguration Bootstrap(EndpointDefinition endpointDefinition);

        void SendCommand(Guid messageId);
        void SendRequest(Guid requestId);
        void PublishEvent(Guid eventId);

        void SendAndCallbackForInt(int value);
        void SendAndCallbackForEnum(CallbackEnum value);

        Guid[] ReceivedMessageIds { get; }
        Guid[] ReceivedResponseIds { get; }
        Guid[] ReceivedEventIds { get;  }

        int[] ReceivedIntCallbacks { get; }
        CallbackEnum[] ReceivedEnumCallbacks { get; }

        int NumberOfSubscriptions { get; }
    }
}
