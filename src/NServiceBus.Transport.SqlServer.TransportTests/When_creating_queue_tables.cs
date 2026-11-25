namespace NServiceBus.TransportTests;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Transport;
using Transport.SqlServer;

//HINT: This test verifies the tables and the startup diagnostics, which the transport seam does not expose.
//      Therefore we are not using the transport base class.
public class When_creating_queue_tables
{
    [TestCase(true, "Index_Bucket_RowVersion")]
    [TestCase(false, "HEAP")]
    public async Task Should_use_the_layout_selected_by_the_setting(bool createBucketedQueueTables, string expectedLayout)
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var prefix = $"CreatingQueueTables{createBucketedQueueTables}";
        string[] queueNames = [$"{prefix}Input", $"{prefix}Error", $"{prefix}Destination"];
        tablesToDrop.AddRange(queueNames.Select(name => $"[dbo].[{name}]"));
        await DropTables(cancellationToken);

        var transport = new SqlServerTransport(ConfigureSqlServerTransportInfrastructure.ConnectionString)
        {
            CreateBucketedQueueTables = createBucketedQueueTables,
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly
        };
        transport.DelayedDelivery.TableSuffix = "Delayed";
        transport.Subscriptions.SubscriptionTableName = new SubscriptionTableName($"{prefix}Subscriptions", "dbo");

        var startupDiagnostic = new StartupDiagnosticEntries();
        var hostSettings = new HostSettings(queueNames[0], string.Empty, startupDiagnostic, (_, _, _) => { }, true);
        var receiveSettings = new ReceiveSettings("mainReceiver", new Transport.QueueAddress(queueNames[0]), true, false, queueNames[1]);

        var infrastructure = await transport.Initialize(hostSettings, [receiveSettings], [queueNames[1], queueNames[2]], cancellationToken);
        await infrastructure.Shutdown(cancellationToken);

        tablesToDrop.Add(transport.Testing.DelayedDeliveryQueue);
        tablesToDrop.Add(transport.Testing.SubscriptionTable);

        var layouts = new List<string>();
        foreach (var queueName in queueNames)
        {
            layouts.Add(await GetLayout(queueName, cancellationToken));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(layouts, Is.All.EqualTo(expectedLayout));
            Assert.That(QueueTablesDiagnostics(startupDiagnostic), Is.EqualTo(JsonSerializer.Serialize(new { CreateBucketedQueueTables = createBucketedQueueTables })));
        }
    }

    [TearDown]
    public Task TearDown() => DropTables(CancellationToken.None);

    static async Task<string> GetLayout(string queueName, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(ConfigureSqlServerTransportInfrastructure.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // index_id 0 is the heap and 1 is the clustered index
        command.CommandText = "SELECT ISNULL(name, type_desc COLLATE DATABASE_DEFAULT) FROM sys.indexes WHERE object_id = OBJECT_ID(@table) AND index_id IN (0, 1)";
        command.Parameters.AddWithValue("@table", $"[dbo].[{queueName}]");

        return (string)await command.ExecuteScalarAsync(cancellationToken);
    }

    static string QueueTablesDiagnostics(StartupDiagnosticEntries startupDiagnostic)
    {
        // Core exposes no public way to read the entries back
        var entries = (List<StartupDiagnosticEntries.StartupDiagnosticEntry>)typeof(StartupDiagnosticEntries)
            .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(startupDiagnostic);

        var transportEntry = entries.Single(entry => entry.Name == "NServiceBus.Transport.SqlServer");

        return JsonSerializer.Serialize(((Dictionary<string, object>)transportEntry.Data)["QueueTables"]);
    }

    async Task DropTables(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(ConfigureSqlServerTransportInfrastructure.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var table in tablesToDrop)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {table}";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    readonly List<string> tablesToDrop = [];
}
