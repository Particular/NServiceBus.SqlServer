﻿namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Settings;
    using SqlServer;

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
            var definition = new SqlServerTransport();
            Func<Task<SqlConnection>> factory = async () =>
            {
                var connection = new SqlConnection(connectionString);
                await connection.OpenAsync().ConfigureAwait(false);
                return connection;
            };
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.ConnectionFactoryOverride, factory);
            var pubSubSettings = new SubscriptionSettings();
            pubSubSettings.DisableSubscriptionCache();
            settings.Set(pubSubSettings);
            definition.Initialize(settings, "Invalid-connection-string");
            Assert.Pass();
        }

        string connectionString;
    }
}