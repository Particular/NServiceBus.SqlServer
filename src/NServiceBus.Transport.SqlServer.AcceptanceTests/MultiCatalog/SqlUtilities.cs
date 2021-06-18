namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System;
    using System.Threading.Tasks;

    public static class SqlUtilities
    {
        public static async Task<bool> CheckIfTableExists(string catalogName, string schemaName, string tableName, SqlConnection connection)
        {
            return await RunCommand(connection, async (command) =>
            {
                command.CommandText = "SELECT OBJECT_ID(@tableName, 'U')";
                _ = command.Parameters.Add(new SqlParameter("@tableName", $"[{catalogName}].[{schemaName}].[{tableName}]"));

                var result = await command.ExecuteScalarAsync();

                return !DBNull.Value.Equals(result);
            });
        }

        public static async Task DropTable(string catalogName, string schemaName, string tableName, SqlConnection connection)
        {
            _ = await RunCommand(connection, async (command) =>
            {
                command.CommandText = @"IF OBJECT_ID(@tableName, 'U') IS NOT NULL
                                        EXEC('DROP TABLE ' + @tableName)";

                _ = command.Parameters.Add(new SqlParameter("@tableName", $"[{catalogName}].[{schemaName}].[{tableName}]"));

                return await command.ExecuteNonQueryAsync();
            });
        }

        static async Task<T> RunCommand<T>(SqlConnection connection, Func<SqlCommand, Task<T>> action)
        {
            bool weOpenedTheConnection = false;

            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                    weOpenedTheConnection = true;
                }

                using (var command = connection.CreateCommand())
                {
                    return await action(command);
                }
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
}
