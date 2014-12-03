namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ReceiveResult
    {
        readonly Exception exception;
        readonly TransportMessage message;

        ReceiveResult(Exception exception, TransportMessage message)
        {
            this.exception = exception;
            this.message = message;
        }

        public static ReceiveResult NoMessage()
        {
            return new ReceiveResult(null, null);
        }

        public static ReceiveResult Received(TransportMessage receivedMessage)
        {
            return new ReceiveResult(null, receivedMessage);
        }

        public ReceiveResult FailedProcessing(Exception encounteredException)
        {
            return new ReceiveResult(encounteredException, message);
        }

        public Exception Exception
        {
            get { return exception; }
        }

        public bool HasReceivedMessage
        {
            get { return message != null; }
        }

        public TransportMessage Message
        {
            get { return message; }
        }
    }


}