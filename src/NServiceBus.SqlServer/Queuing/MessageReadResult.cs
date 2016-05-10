namespace NServiceBus.Transport.SQLServer
{
    struct MessageReadResult
    {
        MessageReadResult(Message message, MessageRow poisonMessage)
        {
            Message = message;
            PoisonMessage = poisonMessage;
        }

        public static MessageReadResult NoMessage = new MessageReadResult(null, null);

        public bool IsPoison => PoisonMessage != null;

        public bool Successful => Message != null;

        public Message Message { get; }

        public MessageRow PoisonMessage { get; }

        public static MessageReadResult Poison(MessageRow messageRow)
        {
            return new MessageReadResult(null, messageRow);
        }

        public static MessageReadResult Success(Message message)
        {
            return new MessageReadResult(message, null);
        }
    }
}