namespace NServiceBus.Transport.Sql.Shared
{
    struct MessageReadResult
    {
        MessageReadResult(Message message, MessageRow poisonMessage, long rowVersion)
        {
            Message = message;
            PoisonMessage = poisonMessage;
            RowVersion = rowVersion;
        }

        public static MessageReadResult NoMessage = new MessageReadResult(null, null, 0);

        public bool IsPoison => PoisonMessage != null;

        public bool Successful => Message != null;

        public Message Message { get; }

        public MessageRow PoisonMessage { get; }

        /// <summary>
        /// SqlServer's <c>RowVersion</c> or Postgres's <c>Seq</c>, used to optimize the receive query.
        /// Not part of equality - the message is enough for that
        /// </summary>
        public long RowVersion { get; }

        public static MessageReadResult Poison(MessageRow messageRow, long rowVersion)
        {
            return new MessageReadResult(null, messageRow, rowVersion);
        }

        public static MessageReadResult Success(Message message, long rowVersion)
        {
            return new MessageReadResult(message, null, rowVersion);
        }

        bool Equals(MessageReadResult other) => Equals(Message, other.Message) && Equals(PoisonMessage, other.PoisonMessage);

        public override bool Equals(object obj) => obj is MessageReadResult other && Equals(other);

        public override int GetHashCode() => Message.GetHashCode() ^ PoisonMessage.GetHashCode();

        public static bool operator ==(MessageReadResult a, MessageReadResult b) => a.Equals(b);

        public static bool operator !=(MessageReadResult a, MessageReadResult b) => !(a == b);
    }
}