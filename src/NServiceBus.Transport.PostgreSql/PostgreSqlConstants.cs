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
WHERE rs.id = (SELECT id FROM {0} ORDER BY Seq LIMIT 1 FOR UPDATE SKIP LOCKED)
RETURNING rs.id,
	    CASE WHEN Expires IS NULL
        THEN 0
        WHEN Expires > now() AT TIME ZONE 'UTC' THEN 0 ELSE 1
	    END Expired,
		rs.Headers, rs.Body;
";

    public string MoveDueDelayedMessageText { get; set; } = @"
WITH message as (DELETE FROM {0} WHERE id in (SELECT id from {0} WHERE {0}.Due < now() AT TIME ZONE 'UTC' LIMIT @BatchSize) 
RETURNING id, headers, body)
INSERT into {1} (id, expires, headers, body) SELECT id, NULL, headers, body FROM message;

SELECT now() AT TIME ZONE 'UTC' as UtcNow, Due as NextDue
FROM {0} 
ORDER BY Due LIMIT 1 FOR UPDATE SKIP LOCKED";

    public string PeekText { get; set; } = @"
SELECT COALESCE(cast(max(seq) - min(seq) + 1 AS int), 0) Id FROM {0}";

    public string AddMessageBodyStringColumn { get; set; } = @"
DO $$
BEGIN

IF EXISTS (
   SELECT FROM information_schema.tables 
   WHERE  table_schema = '{0}'
   AND    table_name   = '{1}'
   )
THEN
    RETURN;
END IF;

IF NOT EXISTS (
	SELECT FROM information_schema.columns 
	WHERE  table_schema = '{0}'
	AND table_name='{1}' 
	AND column_name='StringBody'
	)
THEN
    RETURN;
END IF;

PERFORM pg_advisory_xact_lock('{2}');

IF EXISTS (
    SELECT FROM information_schema.columns 
    WHERE  table_schema = '{0}'
    AND table_name='{1}' 
    AND column_name='StringBody'
    )
THEN
    
    RETURN;
END IF;

ALTER TABLE {0}.""{1}""
ADD BodyString text GENERATED ALWAYS AS (encode(Body, 'escape')) STORED;

END
$$";

    public string CreateQueueText { get; set; } = @"
CREATE TABLE IF NOT EXISTS {0} (
    Id uuid NOT NULL PRIMARY KEY,
    Expires TIMESTAMP,
    Headers TEXT NOT NULL,
    Body BYTEA,
    Seq serial NOT NULL UNIQUE
);
";

    public string CreateDelayedMessageStoreText { get; set; } = @"
CREATE TABLE IF NOT EXISTS {0} (
    Id uuid NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY, 
    Headers text NOT NULL,
    Body bytea,
    Due timestamptz NOT NULL
) WITH (fillfactor=100, autovacuum_enabled=off, toast.autovacuum_enabled=off);

CREATE UNIQUE  INDEX ""{1}_Due"" on {0}(Due);
";

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
) WITH (fillfactor=100, autovacuum_enabled=off, toast.autovacuum_enabled=off)
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