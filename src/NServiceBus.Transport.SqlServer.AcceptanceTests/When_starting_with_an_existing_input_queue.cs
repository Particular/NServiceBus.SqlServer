namespace NServiceBus.Transport.SqlServer.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Logging;
using Microsoft.Data.SqlClient;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

// These tests set CreateBucketedQueueTables explicitly, so the test suite switch does not change their outcome
public class When_starting_with_an_existing_input_queue : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_log_once_that_a_heap_keeps_its_layout_when_bucketed_queue_tables_are_enabled()
    {
        await CreateInputQueue(sqlConstants.CreateQueueText);

        var entries = await StartEndpointAndGetSchemaInspectionLogs(createBucketedQueueTables: true);

        Assert.That(entries, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[0].Level, Is.EqualTo(LogLevel.Info));
            Assert.That(entries[0].Message, Does.StartWith($"Table {InputQueueName}@"));
            Assert.That(entries[0].Message, Does.EndWith(" uses the heap layout and keeps it. CreateBucketedQueueTables applies only to queue tables the transport creates."));
        }
    }

    [Test]
    public async Task Should_log_nothing_about_a_bucketed_table_when_bucketed_queue_tables_are_enabled()
    {
        await CreateInputQueue(sqlConstants.CreateBucketedQueueText);

        var entries = await StartEndpointAndGetSchemaInspectionLogs(createBucketedQueueTables: true);

        Assert.That(entries, Is.Empty);
    }

    [Test]
    public async Task Should_log_nothing_about_a_heap_when_bucketed_queue_tables_are_disabled()
    {
        await CreateInputQueue(sqlConstants.CreateQueueText);

        var entries = await StartEndpointAndGetSchemaInspectionLogs(createBucketedQueueTables: false);

        Assert.That(entries, Is.Empty);
    }

    async Task CreateInputQueue(string creationScript)
    {
        var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
        var connectionFactory = new SqlServerDbConnectionFactory(connectionString);
        var addressTranslator = new QueueAddressTranslator(new SqlConnectionStringBuilder(connectionString).InitialCatalog, "dbo", null, null);

        await using (var connection = await connectionFactory.OpenNewConnection())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {addressTranslator.Parse(InputQueueName).QualifiedTableName}";
            await command.ExecuteNonQueryAsync();
        }

        var queueCreator = new QueueCreator(sqlConstants, creationScript, connectionFactory, addressTranslator.Parse);
        await queueCreator.CreateQueueIfNecessary([InputQueueName], null);
    }

    static async Task<ScenarioContext.LogItem[]> StartEndpointAndGetSchemaInspectionLogs(bool createBucketedQueueTables)
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.CustomConfig(c => c.ConfigureSqlServerTransport().CreateBucketedQueueTables = createBucketedQueueTables))
            .Done(c => c.EndpointsStarted)
            .Run();

        return context.Logs.Where(entry => entry.LoggerName == typeof(SchemaInspector).FullName).ToArray();
    }

    static string InputQueueName => Conventions.EndpointNamingConvention(typeof(Endpoint));

    readonly SqlServerConstants sqlConstants = new();

    class Context : ScenarioContext;

    class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() => EndpointSetup<DefaultServer>();
    }
}
