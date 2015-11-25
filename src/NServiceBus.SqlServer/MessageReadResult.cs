namespace NServiceBus.Transports.SQLServer
{
    struct MessageReadResult
    {
        MessageReadResult(SqlMessage message, bool poison, object[] dataRecord)
        {
            Message = message;
            IsPoison = poison;
            DataRecord = dataRecord;
        }

        public static MessageReadResult NoMessage = new MessageReadResult(null, false, null);

        public bool IsPoison { get; }

        public bool Successful => Message != null;

        public SqlMessage Message { get; }

        public object[] DataRecord { get; }

        public static MessageReadResult Poison(object[] record)
        {
            return new MessageReadResult(null, true, record);
        }

        public static MessageReadResult Success(SqlMessage message)
        {
            return new MessageReadResult(message, false, null);
        }
    }
}