namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ReceiveResult
    {
        ReceiveResult(Exception exception, IncomingMessage message)
        {
            Exception = exception;
            Message = message;
        }

        public static ReceiveResult NoMessage()
        {
            return new ReceiveResult(null, null);
        }

        public static ReceiveResult Received(IncomingMessage receivedMessage)
        {
            return new ReceiveResult(null, receivedMessage);
        }

        public ReceiveResult FailedProcessing(Exception encounteredException)
        {
            return new ReceiveResult(encounteredException, Message);
        }

        public Exception Exception { get; }

        public bool HasReceivedMessage => Message != null;

        public IncomingMessage Message { get; }
    }
}