#:package Microsoft.Data.SqlClient@6.1.8
#:property PublishAot=false
#:property Nullable=disable

// Pages kept by a queue table after 8 sessions fill it and 8 sessions drain it.
// usage: dotnet run reclaim.cs -- <sql dir> <out.jsonl> <repetitions>
using System.Text.Json;
using Microsoft.Data.SqlClient;

var sqlDir = args[0];
var output = args[1];
var repetitions = int.Parse(args[2]);
const int rows = 20000;
var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BENCH_CS")) { InitialCatalog = "master" }.ConnectionString;

foreach (var scenario in new[] { (Name: "RCSI off", Rcsi: "OFF", LongTransaction: false), (Name: "RCSI on", Rcsi: "ON", LongTransaction: false), (Name: "RCSI on, long transaction", Rcsi: "ON", LongTransaction: true) })
{
    for (var repetition = 1; repetition <= repetitions; repetition++)
    {
        foreach (var layout in new[] { "heap", "bucketed" })
        {
            var database = $"b2_{layout}";
            await using (var admin = await Open(master))
            {
                await Execute(admin, $"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE {database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {database}; END");
                await Execute(admin, $"CREATE DATABASE {database}");
                await Execute(admin, $"ALTER DATABASE {database} SET RECOVERY SIMPLE");
                await Execute(admin, $"ALTER DATABASE {database} SET READ_COMMITTED_SNAPSHOT {scenario.Rcsi}");
            }

            SqlConnection.ClearAllPools();
            var connectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = database, MaxPoolSize = 50 }.ConnectionString;
            string Read(string file) => File.ReadAllText(Path.Combine(sqlDir, file)).Replace("{0}", $"[{database}].[dbo].[q]").Replace("{1}", $"[{database}]");

            await using var control = await Open(connectionString);
            await Execute(control, "CREATE TABLE dbo.other (Id int NOT NULL); INSERT INTO dbo.other VALUES (0);");
            await using (var create = new SqlCommand("BEGIN TRANSACTION; EXEC sp_executesql @s; COMMIT;", control))
            {
                create.Parameters.AddWithValue("@s", Read($"create-{layout}.sql"));
                await create.ExecuteNonQueryAsync();
            }

            // started before the deletes and has written, so row versioning has to keep versions for it
            SqlConnection longRunning = null;
            if (scenario.LongTransaction)
            {
                longRunning = await Open(connectionString);
                await Execute(longRunning, "BEGIN TRANSACTION; UPDATE dbo.other SET Id = 1;");
            }

            async Task Sample(string point)
            {
                var sample = await Pages(control);
                sample["scenario"] = scenario.Name;
                sample["repetition"] = repetition;
                sample["layout"] = layout;
                sample["point"] = point;
                var json = JsonSerializer.Serialize(sample);
                File.AppendAllText(output, json + Environment.NewLine);
                Console.WriteLine(json);
            }

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Loop(connectionString, $@"SET NOCOUNT ON;
DECLARE @i int = 0, @Id uniqueidentifier, @Body varbinary(max) = CONVERT(varbinary(max), REPLICATE('x', 1024));
WHILE @i < {rows / 8}
BEGIN
    SET @Id = NEWID();
    EXEC sp_executesql @Statement, N'@Id uniqueidentifier, @TimeToBeReceivedMs int, @Headers nvarchar(max), @Body varbinary(max)', @Id, NULL, N'{{}}', @Body;
    SET @i += 1;
END", Read("send.sql"))));
            await Sample("filled");

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Loop(connectionString, @"SET NOCOUNT ON;
WHILE EXISTS (SELECT * FROM dbo.q WITH (READCOMMITTEDLOCK))
BEGIN
    DECLARE @i int = 0;
    WHILE @i < 50 BEGIN EXEC sp_executesql @Statement; SET @i += 1; END
END", Read("receive.sql"))));

            await Sample("drained, 0 s");
            await Task.Delay(TimeSpan.FromSeconds(10));
            await Sample("drained, 10 s");
            await Task.Delay(TimeSpan.FromSeconds(50));
            await Sample("drained, 60 s");

            if (longRunning != null)
            {
                await Execute(longRunning, "COMMIT");
                await longRunning.DisposeAsync();
                await Sample("commit, 0 s");
                await Task.Delay(TimeSpan.FromSeconds(10));
                await Sample("commit, 10 s");
                await Task.Delay(TimeSpan.FromSeconds(50));
                await Sample("commit, 60 s");
            }
        }
    }
}

// rows, and leaf pages and ghost records of each index or heap
static async Task<Dictionary<string, object>> Pages(SqlConnection connection)
{
    await using var command = new SqlCommand(@"
SELECT (SELECT COUNT(*) FROM dbo.q WITH (READCOMMITTEDLOCK)), ISNULL(i.name, 'heap'), s.page_count, s.ghost_record_count + s.version_ghost_record_count
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('dbo.q'), NULL, NULL, 'DETAILED') s
JOIN sys.indexes i ON i.object_id = s.object_id AND i.index_id = s.index_id
WHERE s.index_level = 0 AND s.alloc_unit_type_desc = 'IN_ROW_DATA'
ORDER BY s.index_id", connection) { CommandTimeout = 300 };
    var count = 0;
    var pages = new Dictionary<string, long>();
    var ghosts = new Dictionary<string, long>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        count = reader.GetInt32(0);
        pages[reader.GetString(1)] = reader.GetInt64(2);
        ghosts[reader.GetString(1)] = reader.IsDBNull(3) ? -1 : reader.GetInt64(3);
    }

    return new() { ["rows"] = count, ["leafPages"] = pages, ["ghosts"] = ghosts };
}

static async Task Loop(string connectionString, string sql, string statement)
{
    await using var connection = await Open(connectionString);
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 900 };
    command.Parameters.Add("@Statement", System.Data.SqlDbType.NVarChar, -1).Value = statement;
    await using var reader = await command.ExecuteReaderAsync();
    do
    {
        while (await reader.ReadAsync())
        {
        }
    } while (await reader.NextResultAsync());
}

static async Task<SqlConnection> Open(string connectionString)
{
    var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    return connection;
}

static async Task Execute(SqlConnection connection, string sql)
{
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };
    await command.ExecuteNonQueryAsync();
}
