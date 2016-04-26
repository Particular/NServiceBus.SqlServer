namespace NServiceBus.Transports.SQLServer
{
    struct MessageReadResult
    {
        MessageReadResult(Message message, MessageRow poisonMessage, bool poison)
        {
            Message = message;
            IsPoison = poison;
            PoisonMessage = poisonMessage;
        }

        public static MessageReadResult NoMessage = new MessageReadResult(null, null, false);

        public bool IsPoison { get; }

        public bool Successful => Message != null;

        public Message Message { get; }

        public MessageRow PoisonMessage { get; }

        public static MessageReadResult Poison(MessageRow messageRow)
        {
            return new MessageReadResult(null, messageRow, true);
        }

        public static MessageReadResult Success(Message message)
        {
            return new MessageReadResult(message, null, false);
        }
    }
}