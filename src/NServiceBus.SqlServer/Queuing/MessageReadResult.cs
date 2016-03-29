namespace NServiceBus.Transports.SQLServer
{
    internal struct MessageReadResult
    {
        MessageReadResult(Message message, bool poison, object[] dataRecord)
        {
            Message = message;
            IsPoison = poison;
            DataRecord = dataRecord;
        }

        public static MessageReadResult NoMessage = new MessageReadResult(null, false, null);

        public bool IsPoison { get; }

        public bool Successful => Message != null;

        public Message Message { get; }

        public object[] DataRecord { get; }

        public static MessageReadResult Poison(object[] record)
        {
            return new MessageReadResult(null, true, record);
        }

        public static MessageReadResult Success(Message message)
        {
            return new MessageReadResult(message, false, null);
        }
    }
}