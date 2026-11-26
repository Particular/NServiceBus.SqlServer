#:package Microsoft.Data.SqlClient@6.1.8
#:property PublishAot=false
#:property Nullable=disable

// 8 senders and 8 receivers on one queue; per minute: rates, rows, pages, ghosts and the receive plan hash.
// usage: dotnet run soak.cs -- <sql dir> <out.jsonl> <minutes> <RCSI ON|OFF> <layout> [<layout>...]
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;

var sqlDir = args[0];
var output = args[1];
var minutes = int.Parse(args[2]);
var rcsi = args[3];
var layouts = args[4..];
var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BENCH_CS")) { InitialCatalog = "master", MaxPoolSize = 100 }.ConnectionString;

foreach (var layout in layouts)
{
    var name = $"b4_{layout}";
    await Execute(master, $"IF DB_ID('{name}') IS NOT NULL BEGIN ALTER DATABASE {name} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {name}; END");
    await Execute(master, $"CREATE DATABASE {name}");
    await Execute(master, $"ALTER DATABASE {name} SET RECOVERY SIMPLE");
    await Execute(master, $"ALTER DATABASE {name} SET READ_COMMITTED_SNAPSHOT {rcsi}");
    await Execute(master, $"ALTER DATABASE {name} MODIFY FILE (NAME = {name}, SIZE = 4GB, FILEGROWTH = 512MB)");
    await Execute(master, $"ALTER DATABASE {name} MODIFY FILE (NAME = {name}_log, SIZE = 2GB, FILEGROWTH = 512MB)");
    SqlConnection.ClearAllPools();
    var database = new SqlConnectionStringBuilder(master) { InitialCatalog = name }.ConnectionString;
    string Read(string file) => File.ReadAllText(Path.Combine(sqlDir, file)).Replace("{0}", $"[{name}].[dbo].[q]").Replace("{1}", $"[{name}]");
    await Execute(database, "BEGIN TRANSACTION; EXEC sp_executesql @s; COMMIT;", ("@s", Read($"create-{layout}.sql")));

    var errors = new List<string>();
    var end = TimeSpan.FromMinutes(minutes);
    var watch = Stopwatch.StartNew();
    var workers = Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
    {
        var sender = i < 8;
        try
        {
            await using var connection = new SqlConnection(database);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 0;
            command.CommandText = sender
                ? @"SET NOCOUNT ON;
DECLARE @end datetime2 = DATEADD(second, @Seconds, SYSUTCDATETIME()), @Id uniqueidentifier, @Body varbinary(max) = CONVERT(varbinary(max), REPLICATE('x', 1024));
WHILE SYSUTCDATETIME() < @end
BEGIN
    SET @Id = NEWID();
    EXEC sp_executesql @Statement, N'@Id uniqueidentifier, @TimeToBeReceivedMs int, @Headers nvarchar(max), @Body varbinary(max)', @Id, NULL, N'{}', @Body;
END"
                : @"SET NOCOUNT ON;
DECLARE @end datetime2 = DATEADD(second, @Seconds, SYSUTCDATETIME());
WHILE SYSUTCDATETIME() < @end
BEGIN
    EXEC sp_executesql @Statement;
END";
            command.Parameters.Add("@Statement", SqlDbType.NVarChar, -1).Value = Read(sender ? "send.sql" : "receive.sql");
            command.Parameters.Add("@Seconds", SqlDbType.Int).Value = (int)end.TotalSeconds;
            await using var reader = await command.ExecuteReaderAsync();
            do
            {
                while (await reader.ReadAsync())
                {
                }
            } while (await reader.NextResultAsync());
        }
        catch (Exception e)
        {
            lock (errors)
            {
                errors.Add($"{(sender ? "sender" : "receiver")} at {watch.Elapsed:mm\\:ss}: {e.Message}");
            }

            Console.WriteLine($"{layout} session error: {e.Message}");
        }
    })).ToArray();

    var last = (Time: 0.0, Inserted: 0L, Received: 0L);
    for (var minute = 1; minute <= minutes; minute++)
    {
        // the last sample is taken shortly before the sessions stop
        var at = minute < minutes ? TimeSpan.FromMinutes(minute) : end - TimeSpan.FromSeconds(3);
        await Task.Delay(at > watch.Elapsed ? at - watch.Elapsed : TimeSpan.Zero);

        await using var connection = new SqlConnection(database);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandText = @"
SELECT CONVERT(bigint, IDENT_CURRENT('dbo.q')),
    (SELECT SUM(row_count) FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID('dbo.q') AND index_id IN (0, 1))";
        long inserted, rows;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            inserted = reader.GetInt64(0);
            rows = reader.GetInt64(1);
        }

        var time = watch.Elapsed.TotalSeconds;
        var received = inserted - rows;

        command.CommandText = @"
SELECT ISNULL(i.name, 'heap'), s.page_count, s.ghost_record_count + s.version_ghost_record_count
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('dbo.q'), NULL, NULL, 'SAMPLED') s
JOIN sys.indexes i ON i.object_id = s.object_id AND i.index_id = s.index_id
WHERE s.index_level = 0 AND s.alloc_unit_type_desc = 'IN_ROW_DATA'";
        var pages = new Dictionary<string, long>();
        var ghosts = new Dictionary<string, long>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                pages[reader.GetString(0)] = reader.GetInt64(1);
                ghosts[reader.GetString(0)] = reader.IsDBNull(2) ? -1 : reader.GetInt64(2);
            }
        }

        var hashes = await PlanHashes(connection, @"
SELECT DISTINCT CONVERT(varchar(20), s.query_plan_hash, 1)
FROM sys.dm_exec_query_stats s
CROSS APPLY sys.dm_exec_sql_text(s.sql_handle) t
WHERE CHARINDEX(@Table, t.text) > 0
    AND SUBSTRING(t.text, s.statement_start_offset / 2 + 1, (IIF(s.statement_end_offset = -1, DATALENGTH(t.text), s.statement_end_offset) - s.statement_start_offset) / 2 + 1) LIKE '%DELETE FROM message%'", $"[{name}].[dbo].[q]");

        var sample = new Dictionary<string, object>
        {
            ["rcsi"] = rcsi, ["layout"] = layout, ["minute"] = minute, ["elapsedSeconds"] = Math.Round(time, 1),
            ["insertedPerMinute"] = Math.Round((inserted - last.Inserted) * 60 / (time - last.Time)),
            ["receivedPerMinute"] = Math.Round((received - last.Received) * 60 / (time - last.Time)),
            ["rows"] = rows, ["pages"] = pages, ["ghosts"] = ghosts, ["receivePlanHashes"] = hashes, ["errors"] = errors.Count
        };
        last = (time, inserted, received);
        File.AppendAllText(output, JsonSerializer.Serialize(sample) + Environment.NewLine);
        Console.WriteLine($"rcsi={rcsi} {layout,-9} min {minute,3} ins/min={sample["insertedPerMinute"],7} rcv/min={sample["receivedPerMinute"],7} rows={rows,8} pages={string.Join('/', pages.Values)} ghosts={string.Join('/', ghosts.Values)} plans={string.Join(',', hashes)} errors={errors.Count}");
    }

    await Task.WhenAll(workers);

    // Query Store keeps every plan the receive used, including any between two samples
    await using (var connection = new SqlConnection(database))
    {
        await connection.OpenAsync();
        await Execute(database, "EXEC sys.sp_query_store_flush_db");
        var stored = await PlanHashes(connection, @"
SELECT DISTINCT CONVERT(varchar(20), p.query_plan_hash, 1)
FROM sys.query_store_plan p
JOIN sys.query_store_query q ON q.query_id = p.query_id
JOIN sys.query_store_query_text t ON t.query_text_id = q.query_text_id
WHERE t.query_sql_text LIKE '%DELETE FROM message%' AND CHARINDEX(@Table, t.query_sql_text) > 0", $"[{name}].[dbo].[q]");
        // how often each plan ran, when, and its operators
        var plans = await PlanHashes(connection, @"
WITH XMLNAMESPACES (DEFAULT 'http://schemas.microsoft.com/sqlserver/2004/07/showplan')
SELECT CONCAT(CONVERT(varchar(20), p.query_plan_hash, 1),
    ' executions=', (SELECT SUM(r.count_executions) FROM sys.query_store_runtime_stats r WHERE r.plan_id = p.plan_id),
    ' first=', CONVERT(varchar(8), (SELECT MIN(r.first_execution_time) FROM sys.query_store_runtime_stats r WHERE r.plan_id = p.plan_id), 108),
    ' last=', CONVERT(varchar(8), (SELECT MAX(r.last_execution_time) FROM sys.query_store_runtime_stats r WHERE r.plan_id = p.plan_id), 108),
    ' operators=', (SELECT STRING_AGG(CONCAT(r.op.value('@PhysicalOp', 'varchar(60)'), ' ' + o.obj.value('@Index', 'nvarchar(128)')), '; ') WITHIN GROUP (ORDER BY r.op.value('@NodeId', 'int'))
        FROM (SELECT CONVERT(xml, p.query_plan) AS x) q
        CROSS APPLY q.x.nodes('//RelOp') r(op)
        OUTER APPLY r.op.nodes('./*/Object') o(obj)))
FROM sys.query_store_plan p
JOIN sys.query_store_query q ON q.query_id = p.query_id
JOIN sys.query_store_query_text t ON t.query_text_id = q.query_text_id
WHERE t.query_sql_text LIKE '%DELETE FROM message%' AND CHARINDEX(@Table, t.query_sql_text) > 0", $"[{name}].[dbo].[q]");
        var summary = new Dictionary<string, object> { ["rcsi"] = rcsi, ["layout"] = layout, ["summary"] = true, ["queryStoreReceivePlanHashes"] = stored, ["queryStorePlans"] = plans, ["errorMessages"] = errors };
        File.AppendAllText(output, JsonSerializer.Serialize(summary) + Environment.NewLine);
        Console.WriteLine($"rcsi={rcsi} {layout} done: Query Store receive plan hashes = {string.Join(',', stored)}; session errors = {errors.Count}");
        plans.ForEach(Console.WriteLine);
    }

    // frees the disk space before the next layout runs
    SqlConnection.ClearAllPools();
    await Execute(master, $"ALTER DATABASE {name} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {name};");
}

static async Task<List<string>> PlanHashes(SqlConnection connection, string sql, string table)
{
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };
    command.Parameters.AddWithValue("@Table", table);
    var hashes = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        hashes.Add(reader.GetString(0));
    }

    return hashes;
}

static async Task Execute(string connectionString, string sql, params (string Name, object Value)[] parameters)
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandTimeout = 600;
    command.CommandText = sql;
    foreach (var (name, value) in parameters)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, -1).Value = value;
    }

    await command.ExecuteNonQueryAsync();
}
