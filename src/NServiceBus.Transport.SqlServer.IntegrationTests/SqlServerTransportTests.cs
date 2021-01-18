namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using NUnit.Framework;

    [TestFixture]
    public class SqlServerTransportTests
    {
        [SetUp]
        public void Prepare()
        {
            connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
            }
        }

        [Test]
        public void It_reads_catalog_from_open_connection()
        {
            var definition = new SqlServerTransport
            {
                ConnectionFactory = async () =>
                {
                    var connection = new SqlConnection(connectionString);
                    await connection.OpenAsync().ConfigureAwait(false);
                    return connection;
                },
                ConnectionString = "Invalid-connection-string"
            };
            definition.Subscriptions.DisableSubscriptionCache();

            Assert.Pass();
        }

        string connectionString;
    }
}