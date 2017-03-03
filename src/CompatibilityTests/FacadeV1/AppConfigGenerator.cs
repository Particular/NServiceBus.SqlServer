using System.Collections.Generic;
using System.IO;
using System.Linq;

class AppConfigGenerator
{
    public FileInfo Generate(string connectionString, List<CustomConnectionString> connectionStrings)
    {
        var nodes = CreateConnectionStringNode(null, connectionString);

        nodes = connectionStrings.Aggregate(nodes, (current, m) => current + CreateConnectionStringNode(m.Address, m.ConnectionString));

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


    public string CreateConnectionStringNode(string name, string connectionString)
    {
        var connectionStringAttribute = connectionString;
        var nameAttribute = name != null ? $"NServiceBus/Transport/{name}" : "NServiceBus/Transport";

        return $@"<add name=""{nameAttribute}"" connectionString=""{connectionStringAttribute}"" />";
    }
}
