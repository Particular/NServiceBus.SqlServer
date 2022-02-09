namespace NServiceBus.Transport.SqlServer
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

        public override bool Equals(object obj) => obj is MessageReadResult other && Equals(other);

        public override int GetHashCode() => Message.GetHashCode() ^ PoisonMessage.GetHashCode();

        public static bool operator ==(MessageReadResult a, MessageReadResult b) => a.Equals(b);

        public static bool operator !=(MessageReadResult a, MessageReadResult b) => !(a == b);
    }
}