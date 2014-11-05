namespace NServiceBus.Transports.SQLServer
{
    struct MessageReadResult
    {
        readonly TransportMessage message;
        readonly bool poison;
        readonly object[] dataRecord;

        MessageReadResult(TransportMessage message, bool poison, object[] dataRecord)
        {
            this.message = message;
            this.poison = poison;
            this.dataRecord = dataRecord;
        }

        public static MessageReadResult NoMessage = new MessageReadResult(null, false, null);

        public bool IsPoison
        {
            get { return poison; }
        }

        public bool Successful
        {
            get { return message != null; }
        }

        public TransportMessage Message
        {
            get { return message; }
        }

        public object[] DataRecord
        {
            get { return dataRecord; }
        }

        public static MessageReadResult Poison(object[] record)
        {
            return new MessageReadResult(null, true, record);
        }

        public static MessageReadResult Success(TransportMessage message)
        {
            return new MessageReadResult(message, false, null);
        }

    }
}