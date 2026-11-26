#:package Microsoft.Data.SqlClient@6.1.8
#:property PublishAot=false
#:property Nullable=disable

// Insert throughput, its trend within a run, and bucket balance.
// usage: dotnet run insert.cs -- <sql dir> <out.jsonl> <RCSI ON|OFF> <seconds> <reps> <concurrencies>
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;

var sqlDir = args[0];
var output = args[1];
var rcsi = args[2];
var seconds = int.Parse(args[3]);
var reps = int.Parse(args[4]);
var concurrencies = args[5].Split(',').Select(int.Parse).ToArray();
var layouts = new[] { "heap", "bucketed", "rowversion" };
var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BENCH_CS")) { InitialCatalog = "master", MaxPoolSize = 100 }.ConnectionString;
var database = new SqlConnectionStringBuilder(master) { InitialCatalog = "b3" }.ConnectionString;

await Execute(master, "IF DB_ID('b3') IS NOT NULL BEGIN ALTER DATABASE b3 SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE b3; END");
await Execute(master, "CREATE DATABASE b3");
await Execute(master, "ALTER DATABASE b3 SET RECOVERY SIMPLE");
await Execute(master, $"ALTER DATABASE b3 SET READ_COMMITTED_SNAPSHOT {rcsi}");
await Execute(master, "ALTER DATABASE b3 MODIFY FILE (NAME = b3, SIZE = 4GB, FILEGROWTH = 512MB)");
await Execute(master, "ALTER DATABASE b3 MODIFY FILE (NAME = b3_log, SIZE = 2GB, FILEGROWTH = 512MB)");
SqlConnection.ClearAllPools();

var send = File.ReadAllText(Path.Combine(sqlDir, "send.sql")).Replace("{0}", "[b3].[dbo].[q]");

for (var rep = 1; rep <= reps; rep++)
{
    foreach (var concurrency in concurrencies)
    {
        // rotate which layout goes first so that drift does not favor one
        foreach (var layout in layouts.Skip((rep - 1) % layouts.Length).Concat(layouts.Take((rep - 1) % layouts.Length)))
        {
            var create = File.ReadAllText(Path.Combine(sqlDir, $"create-{layout}.sql")).Replace("{0}", "[b3].[dbo].[q]").Replace("{1}", "[b3]");
            await Execute(database, "DROP TABLE IF EXISTS dbo.q");
            await Execute(database, "BEGIN TRANSACTION; EXEC sp_executesql @s; COMMIT;", ("@s", create));
            await Execute(database, "CHECKPOINT");

            var errors = new List<string>();
            var connections = new List<SqlConnection>();
            for (var i = 0; i < concurrency; i++)
            {
                var connection = new SqlConnection(database);
                await connection.OpenAsync();
                connections.Add(connection);
            }

            var watch = Stopwatch.StartNew();
            var workers = connections.Select(c => Task.Run(async () =>
            {
                try
                {
                    await using var command = c.CreateCommand();
                    command.CommandTimeout = 0;
                    command.CommandText = @"SET NOCOUNT ON;
DECLARE @end datetime2 = DATEADD(second, @Seconds, SYSUTCDATETIME()), @Id uniqueidentifier, @Body varbinary(max) = CONVERT(varbinary(max), REPLICATE('x', 1024));
WHILE SYSUTCDATETIME() < @end
BEGIN
    SET @Id = NEWID();
    EXEC sp_executesql @Statement, N'@Id uniqueidentifier, @TimeToBeReceivedMs int, @Headers nvarchar(max), @Body varbinary(max)', @Id, NULL, N'{}', @Body;
END";
                    command.Parameters.Add("@Statement", SqlDbType.NVarChar, -1).Value = send;
                    command.Parameters.Add("@Seconds", SqlDbType.Int).Value = seconds;
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    lock (errors)
                    {
                        errors.Add(e.Message);
                    }
                }
            })).ToArray();

            // rows inserted per second in each five-second interval
            var intervals = new List<double>();
            var last = (Time: 0.0, Identity: 0L);
            for (var next = 5; next < seconds; next += 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, next - watch.Elapsed.TotalSeconds)));
                var identity = await Scalar<long>(database, "SELECT CONVERT(bigint, IDENT_CURRENT('dbo.q'))");
                var time = watch.Elapsed.TotalSeconds;
                intervals.Add(Math.Round((identity - last.Identity) / (time - last.Time)));
                last = (time, identity);
            }

            await Task.WhenAll(workers);
            watch.Stop();
            foreach (var c in connections)
            {
                await c.DisposeAsync();
            }

            var rows = await Scalar<long>(database, "SELECT COUNT_BIG(*) FROM dbo.q");
            long? bucketSpread = null;
            long? buckets = null;
            if (layout == "bucketed")
            {
                bucketSpread = await Scalar<long>(database, "SELECT MAX(c) - MIN(c) FROM (SELECT COUNT_BIG(*) AS c FROM dbo.q GROUP BY Bucket) x");
                buckets = await Scalar<long>(database, "SELECT COUNT_BIG(DISTINCT Bucket) FROM dbo.q");
            }

            var third = Math.Max(1, intervals.Count / 3);
            var trend = Math.Round(intervals.TakeLast(third).Average() / intervals.Take(third).Average(), 3);
            var result = new Dictionary<string, object>
            {
                ["rcsi"] = rcsi, ["rep"] = rep, ["concurrency"] = concurrency, ["layout"] = layout,
                ["seconds"] = Math.Round(watch.Elapsed.TotalSeconds, 2), ["rows"] = rows,
                ["insertedPerSecond"] = Math.Round(rows / watch.Elapsed.TotalSeconds),
                ["intervals"] = intervals, ["lastThirdOverFirstThird"] = trend,
                ["buckets"] = buckets, ["bucketSpread"] = bucketSpread, ["errors"] = errors
            };
            File.AppendAllText(output, JsonSerializer.Serialize(result) + Environment.NewLine);
            Console.WriteLine($"rcsi={rcsi} rep={rep} c={concurrency} {layout,-9} ins/s={result["insertedPerSecond"],7} trend={trend} buckets={buckets} spread={bucketSpread} errors={errors.Count}");
        }
    }
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

static async Task<T> Scalar<T>(string connectionString, string sql)
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandTimeout = 600;
    command.CommandText = sql;
    return (T)await command.ExecuteScalarAsync();
}
