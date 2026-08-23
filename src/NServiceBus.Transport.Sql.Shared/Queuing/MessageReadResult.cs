namespace NServiceBus.Transport.Sql.Shared
{
    readonly struct MessageReadResult
    {
        MessageReadResult(Message message, MessageRow poisonMessage, long rowVersion)
        {
            Message = message;
            PoisonMessage = poisonMessage;
            RowVersion = rowVersion;
        }

        public static MessageReadResult NoMessage => default;

        public bool IsEmpty => Message is null && PoisonMessage is null;

        public bool IsPoison => PoisonMessage is not null;

        public Message Message { get; }

        public MessageRow PoisonMessage { get; }

        /// <summary>
        /// Position of the consumed row in the queue (RowVersion/Seq column). Only populated by
        /// anchored receives; 0 otherwise.
        /// </summary>
        public long RowVersion { get; }

        public static MessageReadResult Poison(MessageRow messageRow, long rowVersion = 0) => new(null, messageRow, rowVersion);

        public static MessageReadResult Success(Message message, long rowVersion = 0) => new(message, null, rowVersion);
    }
}
