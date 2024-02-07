namespace NServiceBus.Transport.PostgreSql;

using SqlServer;

class PostgreSqlConstants : ISqlConstants
{
    public string PurgeText { get; set; } = "DELETE FROM {0}";
    public string SendTextWithRecoverable { get; set; } = string.Empty; //It should never be used because PostgreSQL dialect does not have Recoverable column
    public string SendText { get; set; } = @"
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

    public string CheckIfTableHasRecoverableText { get; set; } = "SELECT * FROM {0} LIMIT 0 FOR UPDATE SKIP LOCKED;";
    public string StoreDelayedMessageText { get; set; } = string.Empty;
    public string ReceiveText { get; set; } = @"
DELETE FROM
    {0}
USING (
    SELECT Id, 
    CASE WHEN Expires IS NULL
        THEN 0
        WHEN Expires > now() AT TIME ZONE 'UTC' THEN 0 ELSE 1
    END Expired,
    Headers, Body FROM {0} LIMIT 1 FOR UPDATE SKIP LOCKED
) q
WHERE q.id = {0}.id RETURNING q.Id, q.Expired, q.Headers, q.Body;
";
    public string MoveDueDelayedMessageText { get; set; } = @"
CREATE EXTENSION IF NOT EXISTS ""uuid-ossp""; 
	
WITH message as (DELETE FROM {0} WHERE rowversion in (SELECT rowversion from {0} WHERE {0}.Due < now() AT TIME ZONE 'UTC' LIMIT @BatchSize) 
RETURNING headers, body)
INSERT into {1} (id, correlationid, replytoaddress, expires, headers, body) SELECT  uuid_generate_v4(), NULL, NULL, NULL, headers, body FROM message;

SELECT now() AT TIME ZONE 'UTC' as UtcNow, Due as NextDue
FROM {0} 
ORDER BY Due LIMIT 1 FOR UPDATE SKIP LOCKED";
    public string PeekText { get; set; } = @"
SELECT COALESCE(cast(max(RowVersion) - min(RowVersion) + 1 AS int), 0) Id FROM {0}";

    public string AddMessageBodyStringColumn { get; set; } = string.Empty;
    public string CreateQueueText { get; set; } = @"
    CREATE TABLE IF NOT EXISTS {0} (
        Id uuid NOT NULL,
        CorrelationId varchar(255),
        ReplyToAddress varchar(255),
        Expires TIMESTAMP,
        Headers TEXT NOT NULL,
        Body BYTEA,
        RowVersion serial NOT NULL
    );";

    public string CreateDelayedMessageStoreText { get; set; } = @"
CREATE TABLE IF NOT EXISTS {0} (
    Headers text NOT NULL,
    Body bytea,
    Due timestamptz NOT NULL,
    RowVersion bigint not null generated always as identity
);";

    public string PurgeBatchOfExpiredMessagesText { get; set; } = string.Empty;
    public string CheckIfExpiresIndexIsPresent { get; set; } = string.Empty;
    public string CheckIfNonClusteredRowVersionIndexIsPresent { get; set; } = string.Empty;
    public string CheckHeadersColumnType { get; set; } = string.Empty;

    //HINT: https://stackoverflow.com/questions/1766046/postgresql-create-table-if-not-exists
    public string CreateSubscriptionTableText { get; set; } = @"
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
    public string SubscribeText { get; set; } = @"
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

    public string GetSubscribersText { get; set; } = @"
SELECT DISTINCT QueueAddress
FROM {0}
WHERE Topic IN ({1})
";
    public string UnsubscribeText { get; set; } = @"
DELETE FROM {0}
WHERE
    Endpoint = @Endpoint and
    Topic = @Topic";
}