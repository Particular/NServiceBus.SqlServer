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
        /// SqlServer's <c>RowVersion</c> or Postgres's <c>Seq</c>, used to optimize the receive query.
        /// </summary>
        public long RowVersion { get; }

        public static MessageReadResult Poison(MessageRow messageRow, long rowVersion) => new(null, messageRow, rowVersion);

        public static MessageReadResult Success(Message message, long rowVersion) => new(message, null, rowVersion);
    }
}
