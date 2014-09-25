namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ReceiveResult
    {
        public Exception Exception { get; set; }
        public TransportMessage Message { get; set; }
    }
}