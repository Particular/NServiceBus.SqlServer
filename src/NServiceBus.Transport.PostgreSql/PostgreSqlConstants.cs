namespace NServiceBus.Transport.PostgreSql;

using Sql.Shared.Queuing;

class PostgreSqlConstants : ISqlConstants
{
    public string PurgeText { get; set; } = "DELETE FROM {0}";

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

    public string StoreDelayedMessageText { get; set; } = @"
WITH params (DueDate) as (
   values (timestamptz (now() AT TIME ZONE 'UTC') + (@DueAfterDays || ' days ')::INTERVAL + (@DueAfterHours || ' hours ')::INTERVAL + (@DueAfterMinutes || ' mins ')::INTERVAL + (@DueAfterSeconds || ' s ')::INTERVAL + (@DueAfterMilliseconds || ' ms')::INTERVAL)
)
INSERT INTO {0} (
    Headers,
    Body,
    Due)
SELECT @Headers, @Body, DueDate
FROM params;";

    //HINT: this query should no use USING as this can cause multiple rows to be returned event with LIMIT 1 applied.
    //      https://stackoverflow.com/questions/75755863/limit-1-not-respected-when-used-with-for-update-skip-locked-in-postgres-14
    //      https://dba.stackexchange.com/questions/69471/postgres-update-limit-1/69497#69497
    public string ReceiveText { get; set; } = @"
DELETE FROM {0} rs
WHERE rs.id = (SELECT id FROM {0} LIMIT 1 FOR UPDATE SKIP LOCKED)
RETURNING rs.id,
	    CASE WHEN Expires IS NULL
        THEN 0
        WHEN Expires > now() AT TIME ZONE 'UTC' THEN 0 ELSE 1
	    END Expired,
		rs.Headers, rs.Body;
";

    //TODO investigate the purpose and meaning of this extension, can it be bootstrapped, can dbas turn it off, potential prerequisite for us?
    public string MoveDueDelayedMessageText { get; set; } = @"
CREATE EXTENSION IF NOT EXISTS ""uuid-ossp""; 
	
WITH message as (DELETE FROM {0} WHERE seq in (SELECT seq from {0} WHERE {0}.Due < now() AT TIME ZONE 'UTC' LIMIT @BatchSize) 
RETURNING headers, body)
INSERT into {1} (id, correlationid, replytoaddress, expires, headers, body) SELECT  uuid_generate_v4(), NULL, NULL, NULL, headers, body FROM message;

SELECT now() AT TIME ZONE 'UTC' as UtcNow, Due as NextDue
FROM {0} 
ORDER BY Due LIMIT 1 FOR UPDATE SKIP LOCKED";

    public string PeekText { get; set; } = @"
SELECT COALESCE(cast(max(seq) - min(seq) + 1 AS int), 0) Id FROM {0}";

    //TODO: Verify if it is possible in PostgreSQL
    public string AddMessageBodyStringColumn { get; set; } = string.Empty;

    public string CreateQueueText { get; set; } = @"
    CREATE TABLE IF NOT EXISTS {0} (
        Id uuid NOT NULL,
        CorrelationId varchar(255),
        ReplyToAddress varchar(255),
        Expires TIMESTAMP,
        Headers TEXT NOT NULL,
        Body BYTEA,
        Seq serial NOT NULL
    );";

    public string CreateDelayedMessageStoreText { get; set; } = @"
CREATE TABLE IF NOT EXISTS {0} (
    Headers text NOT NULL,
    Body bytea,
    Due timestamptz NOT NULL,
    Seq bigint not null generated always as identity
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
ON CONFLICT (Endpoint, Topic) DO UPDATE SET QueueAddress = @QueueAddress
;";

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