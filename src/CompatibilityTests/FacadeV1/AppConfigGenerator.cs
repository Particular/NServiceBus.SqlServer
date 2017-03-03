using System.IO;
using System.Linq;

namespace SqlServerV1
{
    using TransportCompatibilityTests.Common.SqlServer;

    public class AppConfigGenerator
    {
        public FileInfo Generate(string connectionString, string defaultSchema, MessageMapping[] messageMappings)
        {
            var nodes = CreateConnectionStringNode(null, connectionString, defaultSchema);

            nodes = messageMappings.Aggregate(nodes, (current, m) => current + CreateConnectionStringNode(m.TransportAddress, connectionString, m.Schema));

            var content = $@"<?xml version='1.0' encoding='utf-8'?>
                            <configuration>
                                <connectionStrings>
                                  <clear />
                                  {nodes}
                                </connectionStrings>
                            </configuration>";

            File.WriteAllText("custom-app.config", content);

            return new FileInfo("custom-app.config");
        }


        public string CreateConnectionStringNode(string name, string connectionString, string schemaName)
        {
            var connectionStringAttribute = connectionString + (schemaName != null ? "Queue Schema=" + schemaName : string.Empty);
            var nameAttribute = name != null ? $"NServiceBus/Transport/{name}" : "NServiceBus/Transport";

            return $@"<add name=""{nameAttribute}"" connectionString=""{connectionStringAttribute}"" />";
        }
    }
}