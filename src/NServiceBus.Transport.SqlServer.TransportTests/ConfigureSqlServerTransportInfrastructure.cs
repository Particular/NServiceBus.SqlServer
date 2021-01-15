using System;
using System.Collections.Generic;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;

public class ConfigureSqlServerTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        return new SqlServerTransport();
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, string inputQueueName,
        string errorQueueName)
    {
        sqlServerTransport = (SqlServerTransport)transportDefinition;

        this.inputQueueName = inputQueueName;
        this.errorQueueName = errorQueueName;

#if !NETFRAMEWORK
        if (sqlServerTransport.TransportTransactionMode == TransportTransactionMode.TransactionScope)
        {
            NUnit.Framework.Assert.Ignore("TransactionScope not supported in .NET Core");
        }
#endif
        
        sqlServerTransport.DelayedDelivery.TableSuffix = "Delayed";
        sqlServerTransport.Subscriptions.DisableSubscriptionCache();
        
        connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
        }

        sqlServerTransport.ConnectionString = connectionString;

        var receivers = new[]
        {
            new ReceiveSettings(
                "mainReceiver",
                inputQueueName,
                transportDefinition.SupportsPublishSubscribe,
                true,
                errorQueueName)
        };

        return await sqlServerTransport.Initialize(hostSettings, receivers, new[] {errorQueueName}).ConfigureAwait(false);
    }

    public async Task Cleanup()
    {
        if (string.IsNullOrWhiteSpace(connectionString) == false)
        {
            var delayedQueueAddress = new QueueAddress(inputQueueName, null, new Dictionary<string, string>(),
                sqlServerTransport.DelayedDelivery.TableSuffix);

            var queues = new[]
            {
                errorQueueName,
                inputQueueName,
                sqlServerTransport.ToTransportAddress(delayedQueueAddress)
            };

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                foreach (var queue in new[] {errorQueueName, inputQueueName})
                {
                    using (var comm = conn.CreateCommand())
                    {
                        comm.CommandText = $"IF OBJECT_ID('{queue}', 'U') IS NOT NULL DROP TABLE {queue}";
                        await comm.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }

    string connectionString;
    string inputQueueName;
    string errorQueueName;
    SqlServerTransport sqlServerTransport;
}