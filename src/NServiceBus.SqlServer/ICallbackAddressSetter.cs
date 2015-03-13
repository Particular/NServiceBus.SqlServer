namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Transports.SQLServer.Config;

    class OutgoingCallbackAddressSetter
    {
        readonly string callbackAddress;

        public OutgoingCallbackAddressSetter(string callbackAddress)
        {
            if (callbackAddress == null)
            {
                throw new ArgumentNullException("callbackAddress");
            }
            this.callbackAddress = callbackAddress;
        }

        public void SetCallbackAddress(TransportMessage message)
        {
            message.Headers[CallbackConfig.CallbackHeaderKey] = callbackAddress;
        }
    }
}