namespace NServiceBus.Transport.Sql.Shared
{
    readonly struct MessageReadResult
    {
        MessageReadResult(Message message, MessageRow poisonMessage)
        {
            Message = message;
            PoisonMessage = poisonMessage;
        }

        public static MessageReadResult NoMessage => default;

        public bool IsEmpty => Message is null && PoisonMessage is null;

        public bool IsPoison => PoisonMessage is not null;

        public Message Message { get; }

        public MessageRow PoisonMessage { get; }

        public static MessageReadResult Poison(MessageRow messageRow) => new(null, messageRow);

        public static MessageReadResult Success(Message message) => new(message, null);
    }
}
