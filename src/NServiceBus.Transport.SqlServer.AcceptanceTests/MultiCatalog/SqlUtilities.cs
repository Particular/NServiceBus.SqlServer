namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

public static class SqlUtilities
{
    public static Task<bool> CheckIfTableExists(string catalogName, string schemaName, string tableName, SqlConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogName);
        ArgumentNullException.ThrowIfNull(schemaName);
        ArgumentNullException.ThrowIfNull(tableName);
        ArgumentNullException.ThrowIfNull(connection);

        return RunCommand(connection, async (command, token) =>
        {
            command.CommandText = "SELECT OBJECT_ID(@tableName, 'U')";
            _ = command.Parameters.Add(new SqlParameter("@tableName", $"[{catalogName}].[{schemaName}].[{tableName}]"));

            var result = await command.ExecuteScalarAsync(token);

            return !DBNull.Value.Equals(result);
        }, cancellationToken);
    }

    public static Task DropTable(string catalogName, string schemaName, string tableName, SqlConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogName);
        ArgumentNullException.ThrowIfNull(schemaName);
        ArgumentNullException.ThrowIfNull(tableName);
        ArgumentNullException.ThrowIfNull(connection);

        return RunCommand(connection, async (command, token) =>
        {
            command.CommandText = @"IF OBJECT_ID(@tableName, 'U') IS NOT NULL
                                        EXEC('DROP TABLE ' + @tableName)";

            _ = command.Parameters.Add(new SqlParameter("@tableName", $"[{catalogName}].[{schemaName}].[{tableName}]"));

            return await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }

    static async Task<T> RunCommand<T>(SqlConnection connection, Func<SqlCommand, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);

        bool weOpenedTheConnection = false;

        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                weOpenedTheConnection = true;
            }

            using var command = connection.CreateCommand();

            return await action(command, cancellationToken);
        }
        finally
        {
            if (weOpenedTheConnection)
            {
                connection.Close();
            }
        }
    }
}