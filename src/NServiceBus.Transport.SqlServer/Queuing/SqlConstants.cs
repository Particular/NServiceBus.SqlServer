namespace NServiceBus.Transport.SqlServer
{
    static class SqlConstants
    {
        public static readonly string PurgeText = "DELETE FROM {0}";

        //TODO: Add TTBR handling
        public static readonly string SendText =
            @"
INSERT INTO {0} (
    Id,
    Expires,
    Headers,
    Body)
VALUES (
    @Id,
    NULL,
    @Headers,
    @Body);
";

        public static readonly string StoreDelayedMessageText =
@""; //TODO

        public static readonly string ReceiveText = @"
BEGIN;
DELETE FROM
    {0}
USING (
    SELECT Id, Headers, Body FROM {0} LIMIT 1 FOR UPDATE SKIP LOCKED
) q
WHERE q.id = {0}.id RETURNING {0}.Id, {0}.Headers, {0}.Body;
";

        public static readonly string MoveDueDelayedMessageText = @""; //TODO

        public static readonly string PeekText = @"
SELECT COALESCE(cast(max(RowVersion) - min(RowVersion) + 1 AS int), 0) Id FROM {0}";

        public static readonly string AddMessageBodyStringColumn = @""; //TODO

        public static readonly string CreateQueueText = @"
CREATE TABLE IF NOT EXISTS {0} (
    Id uuid NOT NULL,
    CorrelationId varchar(255),
    ReplyToAddress varchar(255),
    Expires timestamptz,
    Headers text NOT NULL,
    Body bytea,
    RowVersion bigint not null generated always as identity
);
";

        public static readonly string CreateDelayedMessageStoreText = @"
CREATE TABLE IF NOT EXISTS {0} (
    Headers text NOT NULL,
    Body bytea,
    Due timestamptz NOT NULL,
    RowVersion bigint not null generated always as identity
);
";

        public static readonly string PurgeBatchOfExpiredMessagesText = @"
DELETE FROM {0}
WHERE RowVersion
    IN (SELECT TOP (@BatchSize) RowVersion
        FROM {0} WITH (READPAST)
        WHERE Expires < GETUTCDATE())";

        public static readonly string CreateSubscriptionTableText = @"
CREATE TABLE IF NOT EXISTS {0} (
    QueueAddress VARCHAR(200) NOT NULL,
    Endpoint VARCHAR(200) NOT NULL,
    Topic VARCHAR(200) NOT NULL,
    PRIMARY KEY
    (
        Endpoint,
        Topic
    )
)
";

        public static readonly string SubscribeText = @"
INSERT INTO {0}
(
    QueueAddress,
    Topic,
    Endpoint
)
VALUES
(
    @QueueAddress,
    @Topic,
    @Endpoint
)
ON CONFLICT DO NOTHING
;"; //TODO: Probably need to do update

        public static readonly string GetSubscribersText = @"
SELECT DISTINCT QueueAddress
FROM {0}
WHERE Topic IN ({1})
";

        public static readonly string UnsubscribeText = @"
DELETE FROM {0}
WHERE
    Endpoint = @Endpoint and
    Topic = @Topic";

    }
}
