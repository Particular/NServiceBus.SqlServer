namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Routing;
    using Transport.SQLServer;

    public class AutoDetectQueueLocationTests
    {
        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
        static SqlConnectionFactory sqlConnectionFactory1 = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True");

        static readonly Dictionary<string, string> correctColumns = new Dictionary<string, string>
        {
            {"Id", Id_Correct},
            {"CorrelationId", CorrelationId_Correct},
            {"ReplyToAddress", ReplyToAddress_Correct},
            {"Recoverable", Recoverable_Correct},
            {"Expires", Expires_Correct},
            {"Headers", Headers_Correct},
            {"Body", Body_Correct},
            {"RowVersion", RowVersion_Correct},
        };

        [TestCase]
        public async Task Tables_with_matching_colum_names_are_ignored()
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureTableCreated(connection, "[s1].[Valid]", correctColumns.Values);

            var detector = new AutoDetectQueueLocation(new[] { "nservicebus"}, new[] {"s1"}, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            var detected = result[0];
            Assert.AreEqual("Valid", detected.Endpoint);
            Assert.AreEqual("s1", detected.Properties[SettingsKeys.SchemaPropertyKey]);
            Assert.AreEqual("nservicebus", detected.Properties[SettingsKeys.CatalogPropertyKey]);
        }

        [TestCase]
        public async Task Views_with_matching_colum_names_are_ignored()
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureTableCreated(connection, "[s1].[Valid]", correctColumns.Values);
            await EnsureViewCreated(connection, "[s1].[Valid]", "[s1].[ValidView]");

            var detector = new AutoDetectQueueLocation(new[] { "nservicebus" }, new[] { "s1" }, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            Assert.IsFalse(result.Any(i => string.Equals(i.Endpoint, "ValidView", StringComparison.InvariantCultureIgnoreCase)));
        }

        [TestCase]
        public async Task Tables_in_non_scanned_schemas_are_ignored()
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureSchemaCreated(connection, "s2");
            await EnsureTableCreated(connection, "[s1].[Valid]", correctColumns.Values);

            var detector = new AutoDetectQueueLocation(new[] { "nservicebus" }, new[] { "s2" }, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            Assert.IsFalse(result.Any(i => string.Equals(i.Endpoint, "Valid", StringComparison.InvariantCultureIgnoreCase)));
        }

        [TestCase]
        public async Task Tables_in_non_scanned_catalogs_are_ignored()
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureSchemaCreated(connection, "s2");
            await EnsureTableCreated(connection, "[s1].[Valid]", correctColumns.Values);

            var detector = new AutoDetectQueueLocation(new[] { "nservicebus1" }, null, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            Assert.IsFalse(result.Any(i => string.Equals(i.Endpoint, "Valid", StringComparison.InvariantCultureIgnoreCase)));
        }

        [TestCase]
        public async Task When_schema_filter_is_missing_all_schemas_are_included()
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureTableCreated(connection, "[s1].[Valid]", correctColumns.Values);
            await EnsureTableCreated(connection, "[s2].[Valid2]", correctColumns.Values);

            var detector = new AutoDetectQueueLocation(new[] { "nservicebus" }, null, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            Assert.IsTrue(result.Any(i => string.Equals(i.Endpoint, "Valid", StringComparison.InvariantCultureIgnoreCase)));
            Assert.IsTrue(result.Any(i => string.Equals(i.Endpoint, "Valid2", StringComparison.InvariantCultureIgnoreCase)));
        }

        [TestCase]
        public async Task When_catalog_filter_is_missing_all_catalogs_are_included()
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();
            var connection1 = await sqlConnectionFactory1.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureSchemaCreated(connection1, "s1");
            await EnsureTableCreated(connection, "[s1].[Valid]", correctColumns.Values);
            await EnsureTableCreated(connection1, "[s1].[Valid2]", correctColumns.Values);

            var detector = new AutoDetectQueueLocation(null, null, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            Assert.IsTrue(result.Any(i => string.Equals(i.Endpoint, "Valid", StringComparison.InvariantCultureIgnoreCase)));
            Assert.IsTrue(result.Any(i => string.Equals(i.Endpoint, "Valid2", StringComparison.InvariantCultureIgnoreCase)));
        }

        [TestCase]
        public async Task Tables_with_non_matching_colum_names_are_ignored()
        {
            foreach (var column in correctColumns.Keys)
            {
                await AssertDetectsMissingColumn(column);
                await AssertDetectsInvalidColumnType(column);
            }
        }

        [TestCase]
        public async Task Throws_when_encounters_duplicate_location()
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "d1");
            await EnsureSchemaCreated(connection, "d2");
            await EnsureTableCreated(connection, "[d1].[Duplicate]", correctColumns.Values);
            await EnsureTableCreated(connection, "[d2].[Duplicate]", correctColumns.Values);

            var detector = new AutoDetectQueueLocation(new[] { "nservicebus" }, new[] { "d1", "d2" }, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);

            Assert.That(() => detector.UpdateInstanceTable(true).GetAwaiter().GetResult(), Throws.Exception.Message.Contains("Duplicate location detected for Duplicate"));
        }

        async Task AssertDetectsMissingColumn(string columnName)
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureTableCreated(connection, "[s1].[Missing_"+columnName+"]", Substitute(correctColumns, columnName, "[_" + columnName + "] [uniqueidentifier] NOT NULL").Values);

            var detector = new AutoDetectQueueLocation(new[]
            {
                "nservicebus"
            }, new[]
            {
                "s1"
            }, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            Assert.IsFalse(result.Any(i => string.Equals(i.Endpoint, "Missing_"+columnName, StringComparison.InvariantCultureIgnoreCase)));
        }

        async Task AssertDetectsInvalidColumnType(string columnName)
        {
            var connection = await sqlConnectionFactory.OpenNewConnection();

            await EnsureSchemaCreated(connection, "s1");
            await EnsureTableCreated(connection, "[s1].[Invalid_" + columnName + "]", Substitute(correctColumns, columnName, "[" + columnName + "] [sql_variant] NULL").Values);

            var detector = new AutoDetectQueueLocation(new[]
            {
                "nservicebus"
            }, new[]
            {
                "s1"
            }, sqlConnectionFactory, new EndpointInstances(), TimeSpan.Zero);
            var result = await detector.Detect();

            Assert.IsFalse(result.Any(i => string.Equals(i.Endpoint, "Invalid_" + columnName, StringComparison.InvariantCultureIgnoreCase)));
        }

        Dictionary<string, string> Substitute(Dictionary<string, string> source, string key, string newValue)
        {
            var result = new Dictionary<string, string>(source)
            {
                [key] = newValue
            };
            return result;
        }
        Task EnsureTableCreated(SqlConnection connection, string tableName, IEnumerable<string> columns)
        {
            var columnsString = string.Join(", ", columns);
            var command = new SqlCommand(string.Format(CreateQueueText, tableName, columnsString), connection);
            return command.ExecuteNonQueryAsync();
        }

        Task EnsureViewCreated(SqlConnection connection, string sourceTableName, string viewName)
        {
            var command = new SqlCommand(string.Format(CreateViewText, sourceTableName, viewName), connection);
            return command.ExecuteNonQueryAsync();
        }

        Task EnsureSchemaCreated(SqlConnection connection, string schemaName)
        {
            var command = new SqlCommand(string.Format(CreateSchemaText, schemaName), connection);
            return command.ExecuteNonQueryAsync();
        }

        const string Id_Correct = @"[Id] [uniqueidentifier] NOT NULL";
        const string CorrelationId_Correct = @"[CorrelationId] [varchar](255) NULL";
        const string ReplyToAddress_Correct = @"[ReplyToAddress] [varchar](255) NULL";
        const string Recoverable_Correct = @"[Recoverable] [bit] NOT NULL";
        const string Expires_Correct = @"[Expires] [datetime] NULL";
        const string Headers_Correct = @"[Headers] [varchar](max) NOT NULL";
        const string Body_Correct = @"[Body] [varbinary](max) NULL";
        const string RowVersion_Correct = @"[RowVersion] [bigint] IDENTITY(1,1) NOT NULL";

        const string CreateSchemaText =
@"IF NOT EXISTS (
SELECT  schema_name
FROM    information_schema.schemata
WHERE   schema_name = '{0}' ) 

BEGIN
EXEC sp_executesql N'CREATE SCHEMA {0}'
END";

        const string CreateQueueText =
@"IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))
BEGIN
    DROP TABLE {0};
END
CREATE TABLE {0}({1}) ON [PRIMARY];";

        const string CreateViewText =
@"IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{1}') AND type in (N'V'))
BEGIN
    DROP VIEW {1};
END

EXEC sp_executesql N'CREATE VIEW {1} AS SELECT * FROM {0}'";
    }
}