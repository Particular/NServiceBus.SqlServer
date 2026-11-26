#:package Microsoft.Data.SqlClient@6.1.8
#:property PublishAot=false
#:property Nullable=disable

// Cached plans and logical reads of every queue statement, per layout and table state.
// usage: dotnet run plans.cs -- <sql dir> <out.json> <RCSI: ON|OFF> <mode: states|loops> [rows, default 100000]
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

var sqlDir = args[0];
var output = args[1];
var rcsi = args[2];
var mode = args[3];
var rows = args.Length > 4 ? int.Parse(args[4]) : 100000;
var planDir = Path.Combine(Path.GetDirectoryName(output), "plans");
Directory.CreateDirectory(planDir);
var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BENCH_CS")) { InitialCatalog = "master" }.ConnectionString;
var results = new List<Dictionary<string, object>>();
XNamespace ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";
void Save() => File.WriteAllText(output, JsonSerializer.Serialize(results));

foreach (var layout in new[] { "heap", "bucketed", "persisted" })
{
    var database = $"b1_{layout}";
    await using (var admin = await Open(master))
    {
        await Execute(admin, $"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE {database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {database}; END");
        await Execute(admin, $"CREATE DATABASE {database}");
        await Execute(admin, $"ALTER DATABASE {database} SET RECOVERY SIMPLE");
        await Execute(admin, $"ALTER DATABASE {database} SET READ_COMMITTED_SNAPSHOT {rcsi}");
    }

    SqlConnection.ClearAllPools();
    var connectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;
    string Read(string file) => File.ReadAllText(Path.Combine(sqlDir, file)).Replace("{0}", $"[{database}].[dbo].[q]").Replace("{1}", $"[{database}]");
    var queue = $"[{database}].[dbo].[q]";
    var delayed = $"[{database}].[dbo].[d]";
    var send = Read("send.sql");
    var receive = Read("receive.sql");
    var statements = new (string Name, string Text, string Parameters)[]
    {
        ("receive", receive, null),
        ("peek", Read("peek.sql"), null),
        ("purge expired", Read("purge-expired.sql"), "@BatchSize int = 10000"),
        ("send", send, "@Id uniqueidentifier = '00000000-0000-0000-0000-000000000001', @TimeToBeReceivedMs int = NULL, @Headers nvarchar(max) = N'{}', @Body varbinary(max) = 0x01"),
        ("mover", File.ReadAllText(Path.Combine(sqlDir, "mover.sql")).Replace("{0}", delayed).Replace("{1}", queue), "@BatchSize int = 100"),
        ("mover with lock", File.ReadAllText(Path.Combine(sqlDir, "mover-lock.sql")).Replace("{0}", delayed).Replace("{1}", queue).Replace("{2}", $"[{database}]"), "@BatchSize int = 100, @MoverLockResource nvarchar(255) = N'b1-mover', @MoverLockCatalog nvarchar(128) = N'" + database + "'")
    };

    var states = mode == "states" ? new[] { "100000 rows", "1 row", "1 row after draining 100000", "1 row after draining 100000, 60 s later" } : [];
    foreach (var state in states)
    {
        await using var connection = await Open(connectionString);
        if (state != "1 row after draining 100000, 60 s later")
        {
            await Execute(connection, "DROP TABLE IF EXISTS dbo.q, dbo.d");
            await Execute(connection, "BEGIN TRANSACTION; EXEC sp_executesql @s; COMMIT;", ("@s", Read($"create-{layout}.sql")));
            await Execute(connection, "BEGIN TRANSACTION; EXEC sp_executesql @s; COMMIT;", ("@s", File.ReadAllText(Path.Combine(sqlDir, "create-delayed.sql")).Replace("{0}", delayed).Replace("{1}", $"[{database}]")));
            // 100 delayed messages are due, so that each mover run inserts 100 rows into the queue
            await Execute(connection, "INSERT INTO dbo.d (Headers, Body, Due) SELECT TOP (100) N'{}', CONVERT(varbinary(max), REPLICATE('x', 1024)), DATEADD(hour, -1, GETUTCDATE()) FROM sys.all_columns");

            if (state != "1 row")
            {
                // every hundredth message is expired
                await Execute(connection, $@"
DECLARE @done int = 0;
WHILE @done < {rows}
BEGIN
    INSERT INTO dbo.q (Id, Recoverable, Expires, Headers, Body)
    SELECT TOP (IIF({rows} - @done < 5000, {rows} - @done, 5000)) NEWID(), 1, IIF(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 100 = 0, DATEADD(hour, -1, GETUTCDATE()), NULL), N'{{}}', CONVERT(varbinary(max), REPLICATE('x', 1024))
    FROM sys.all_columns a CROSS JOIN sys.all_columns b;
    SET @done += 5000;
END");
            }

            if (state.StartsWith("1 row after"))
            {
                await Execute(connection, @"
WHILE EXISTS (SELECT * FROM dbo.q WITH (READCOMMITTEDLOCK))
BEGIN
    DECLARE @i int = 0;
    WHILE @i < 1000 BEGIN EXEC sp_executesql @Statement; SET @i += 1; END
END", ("@Statement", receive));
            }

            if (state != "100000 rows")
            {
                await Execute(connection, "INSERT INTO dbo.q (Id, Recoverable, Headers, Body) VALUES (NEWID(), 1, N'{}', CONVERT(varbinary(max), REPLICATE('x', 1024)))");
            }
        }
        else
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
        }

        await Execute(connection, "UPDATE STATISTICS dbo.q; ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE");
        var pages = await Scalar<string>(connection, @"
SELECT STRING_AGG(CONCAT(ISNULL(i.name, 'heap'), ': ', s.page_count, ' pages, depth ', s.index_depth, ', ghosts ', s.ghost_record_count), '; ') WITHIN GROUP (ORDER BY s.index_id)
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('dbo.q'), NULL, NULL, 'DETAILED') s
JOIN sys.indexes i ON i.object_id = s.object_id AND i.index_id = s.index_id
WHERE s.index_level = 0 AND s.alloc_unit_type_desc = 'IN_ROW_DATA'");

        foreach (var statement in statements)
        {
            var reads = new Dictionary<string, long>();
            void OnMessage(object _, SqlInfoMessageEventArgs e)
            {
                foreach (Match match in Regex.Matches(e.Message, @"Table '([^']+)'\. Scan count \d+, logical reads (\d+).*?lob logical reads (\d+)"))
                {
                    reads[match.Groups[1].Value] = reads.GetValueOrDefault(match.Groups[1].Value) + long.Parse(match.Groups[2].Value);
                    if (long.Parse(match.Groups[3].Value) > 0)
                    {
                        reads[match.Groups[1].Value + " (lob)"] = reads.GetValueOrDefault(match.Groups[1].Value + " (lob)") + long.Parse(match.Groups[3].Value);
                    }
                }
            }

            // rolled back, so that every statement sees the same table
            connection.InfoMessage += OnMessage;
            var declarations = statement.Parameters == null ? "" : ", N'" + string.Join(", ", Regex.Matches(statement.Parameters, @"(@\w+) ([\w()]+)").Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value}")) + "', "
                + string.Join(", ", Regex.Matches(statement.Parameters, @"(@\w+) [\w()]+ = (N?'[^']*'|[^,\s]+)").Select(m => $"{m.Groups[1].Value} = {m.Groups[2].Value}"));
            await Execute(connection, $"SET NOCOUNT ON; BEGIN TRANSACTION; SET STATISTICS IO ON; EXEC sp_executesql @Statement{declarations}; SET STATISTICS IO OFF; ROLLBACK;", ("@Statement", statement.Text));
            connection.InfoMessage -= OnMessage;

            results.Add(new() { ["rcsi"] = rcsi, ["layout"] = layout, ["state"] = state, ["statement"] = statement.Name, ["reads"] = reads, ["pages"] = pages });
        }

        // the cached plans, which are the plans the statements ran with
        var planTexts = new List<string>();
        await using (var command = new SqlCommand(@"
SELECT CONVERT(nvarchar(max), p.query_plan) FROM sys.dm_exec_cached_plans c
CROSS APPLY sys.dm_exec_query_plan(c.plan_handle) p
CROSS APPLY sys.dm_exec_sql_text(c.plan_handle) t
WHERE p.query_plan IS NOT NULL AND CHARINDEX(@Database, t.text) > 0 AND t.text NOT LIKE '%dm_exec_cached_plans%'", connection))
        {
            command.Parameters.AddWithValue("@Database", $"[{database}]");
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                planTexts.Add(reader.GetString(0));
            }
        }

        foreach (var planText in planTexts)
        {
            foreach (var plan in XDocument.Parse(planText).Descendants(ns + "StmtSimple"))
            {
                var text = (string)plan.Attribute("StatementText") ?? "";
                var name = text.Contains("deleted.Expires IS NULL") ? "receive"
                    : text.Contains("max([RowVersion])") ? "peek"
                    : text.Contains("@BatchSize) RowVersion") ? "purge expired"
                    : text.Contains("@Id,") && text.Contains("INSERT INTO") ? "send"
                    : text.Contains("NEWID()") ? (planText.Contains("sp_getapplock") ? "mover with lock" : "mover")
                    : null;
                if (name == null)
                {
                    continue;
                }

                var operators = new List<string>();
                foreach (var relOp in plan.Descendants(ns + "RelOp"))
                {
                    foreach (var detail in relOp.Elements())
                    {
                        foreach (var target in detail.Elements(ns + "Object").Where(o => (string)o.Attribute("Table") == "[q]" && (string)o.Attribute("Database") == $"[{database}]"))
                        {
                            var index = (string)target.Attribute("Index");
                            var lookup = (string)detail.Attribute("Lookup") is "1" or "true";
                            var ordered = (string)detail.Attribute("Ordered") is "1" or "true";
                            var physical = (string)relOp.Attribute("PhysicalOp");
                            operators.Add($"{(lookup ? (index == null ? "RID Lookup" : "Key Lookup") : physical)} {(index ?? "(heap)").Trim('[', ']')}{(ordered ? " ordered" : "")}");
                        }
                    }
                }

                if (operators.Count == 0)
                {
                    continue;
                }

                File.WriteAllText(Path.Combine(planDir, $"rcsi-{rcsi}-{layout}-{state}-{name}.sqlplan".Replace(' ', '_')), planText);
                var computeScalars = plan.Descendants(ns + "RelOp").Count(r => (string)r.Attribute("PhysicalOp") == "Compute Scalar");
                foreach (var row in results.Where(r => (string)r["state"] == state && (string)r["layout"] == layout && (string)r["statement"] == name))
                {
                    row["operators"] = operators;
                    row["computeScalars"] = computeScalars;
                }
            }
        }

        Save();
        Console.WriteLine($"rcsi={rcsi} {layout} | {state} | {pages}");
    }
}

if (mode == "loops")
{
    // committed statements in a server-side loop: reads and CPU per execution; layouts alternate within each repetition
    foreach (var repetition in new[] { 1, 2, 3 })
    {
        foreach (var layout in new[] { "heap", "bucketed", "persisted" })
        {
            var database = $"b1_{layout}";
            SqlConnection.ClearAllPools();
            var connectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;
            string Read(string file) => File.ReadAllText(Path.Combine(sqlDir, file)).Replace("{0}", $"[{database}].[dbo].[q]").Replace("{1}", $"[{database}]");
            await using var connection = await Open(connectionString);
            await Execute(connection, "DROP TABLE IF EXISTS dbo.q");
            await Execute(connection, "BEGIN TRANSACTION; EXEC sp_executesql @s; COMMIT;", ("@s", Read($"create-{layout}.sql")));
            foreach (var (name, text, loop, count) in new[]
            {
                ($"send x {rows * 6 / 10}", Read("send.sql"), "SET @Id = NEWID(); EXEC sp_executesql @Statement, N'@Id uniqueidentifier, @TimeToBeReceivedMs int, @Headers nvarchar(max), @Body varbinary(max)', @Id, NULL, N'{}', @Body;", rows * 6 / 10),
                ($"receive x {rows * 4 / 10}", Read("receive.sql"), "EXEC sp_executesql @Statement;", rows * 4 / 10)
            })
            {
                var sql = $@"SET NOCOUNT ON;
DECLARE @i int = 0, @Id uniqueidentifier, @Body varbinary(max) = CONVERT(varbinary(max), REPLICATE('x', 1024));
WHILE @i < {count} BEGIN {loop} SET @i += 1; END";
                var before = await Session(connection);
                await Execute(connection, sql, ("@Statement", text));
                var after = await Session(connection);
                var row = new Dictionary<string, object>
                {
                    ["rcsi"] = rcsi, ["layout"] = layout, ["state"] = $"committed loop, repetition {repetition}", ["statement"] = name,
                    ["readsPerExecution"] = Math.Round((after.Reads - before.Reads) / (double)count, 3),
                    ["cpuMicrosecondsPerExecution"] = Math.Round((after.Cpu - before.Cpu) * 1000.0 / count, 1)
                };
                results.Add(row);
                Save();
                Console.WriteLine($"rcsi={rcsi} rep {repetition} {layout} {name}: reads {row["readsPerExecution"]}, cpu us {row["cpuMicrosecondsPerExecution"]}");
            }
        }
    }
}

static async Task<SqlConnection> Open(string connectionString)
{
    var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    return connection;
}

static async Task Execute(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
{
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 900 };
    foreach (var (name, value) in parameters)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, -1).Value = value;
    }

    // consume every result set, such as the rows a receive returns
    await using var reader = await command.ExecuteReaderAsync();
    do
    {
        while (await reader.ReadAsync())
        {
        }
    } while (await reader.NextResultAsync());
}

static async Task<T> Scalar<T>(SqlConnection connection, string sql)
{
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };
    return (T)await command.ExecuteScalarAsync();
}

static async Task<(long Reads, long Cpu)> Session(SqlConnection connection)
{
    await using var command = new SqlCommand("SELECT logical_reads, CONVERT(bigint, cpu_time) FROM sys.dm_exec_sessions WHERE session_id = @@SPID", connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    return (reader.GetInt64(0), reader.GetInt64(1));
}
