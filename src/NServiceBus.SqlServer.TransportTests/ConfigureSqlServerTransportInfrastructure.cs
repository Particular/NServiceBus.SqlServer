using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.Transport;
using NServiceBus.TransportTests;

public class ConfigureSqlServerTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transportTransactionMode)
    {
        this.settings = settings;
        settings.Set("NServiceBus.SharedQueue", settings.EndpointName());
        return new TransportConfigurationResult
        {
            TransportInfrastructure = new SqlServerTransport().Initialize(settings, ConnectionString)
        };
    }

    public async Task Cleanup()
    {
        var queueBindings = settings.Get<QueueBindings>();
        var queueNames = new List<string>();

        using (var conn = new SqlConnection(ConnectionString))
        {
            await conn.OpenAsync();

            var qn = queueBindings.ReceivingAddresses.ToList();
            qn.ForEach(n =>
            {
                var nameParts = n.Split('@');
                if (nameParts.Length == 2)
                {
                    using (var sanitizer = new SqlCommandBuilder())
                    {
                        var sanitizedSchemaName = SanitizeIdentifier(nameParts[1], sanitizer);
                        var sanitizedTableName = SanitizeIdentifier(nameParts[0], sanitizer);

                        queueNames.Add($"{sanitizedSchemaName}.{sanitizedTableName}");
                    }
                }
                else
                {
                    queueNames.Add(n);
                }
            });
            foreach (var queue in queueNames)
            {
                using (var comm = conn.CreateCommand())
                {
                    comm.CommandText = $"IF OBJECT_ID('{queue}', 'U') IS NOT NULL DROP TABLE {queue}";
                    await comm.ExecuteNonQueryAsync();
                }
            }
        }
    }

    static string SanitizeIdentifier(string identifier, SqlCommandBuilder sanitizer)
    {
        // Identifier may initially quoted or unquoted.
        return sanitizer.QuoteIdentifier(sanitizer.UnquoteIdentifier(identifier));
    }

    SettingsHolder settings;
    const string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
}