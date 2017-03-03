namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Data.SqlClient;
    using Common;
    using NUnit.Framework;

    [TestFixture]
    public abstract class SqlServerContext
    {
        [SetUp]
        public void CommonSetup()
        {
            if (string.IsNullOrWhiteSpace(SqlServerConnectionStringBuilder.Build()))
            {
                throw new Exception($"Environment variables `{SqlServerConnectionStringBuilder.EnvironmentVariable}` are required to connect to Sql Server.");
            }

            var builder = new SqlConnectionStringBuilder(SqlServerConnectionStringBuilder.Build());
            var initialCatalog = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                connection.Open();

                var query = @"IF EXISTS(SELECT * from sys.databases where name = '{0}')
                              BEGIN 
                                ALTER DATABASE [{0}] SET  SINGLE_USER WITH ROLLBACK IMMEDIATE
                                DROP DATABASE [{0}]
                              END 
                            CREATE DATABASE [{0}]";

                command.CommandText = string.Format(query, initialCatalog);
                command.ExecuteNonQuery();
            }

            using (var connection = new SqlConnection(SqlServerConnectionStringBuilder.Build()))
            using (var command = connection.CreateCommand())
            {
                connection.Open();

                var commandText = "CREATE SCHEMA {0}";

                command.CommandText = string.Format(commandText, SourceSchema);
                command.ExecuteNonQuery();

                command.CommandText = string.Format(commandText, DestinationSchema);
                command.ExecuteNonQuery();
            }
        }

        public static string SourceSchema = "src";
        public static string DestinationSchema = "dest";
    }
}
