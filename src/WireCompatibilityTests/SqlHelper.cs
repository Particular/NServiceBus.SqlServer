using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

public static class SqlHelper
{
    public static async Task<int> ExecuteSql(string connectionString, string sql, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseExists(connectionString, cancellationToken).ConfigureAwait(false);

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task CreateSchema(string connectionString, string schema, CancellationToken cancellationToken = default)
    {
        var sql = $@"
if not exists (select  *
               from    sys.schemas
               where   name = N'{schema}')
    exec('create schema {schema}');";
        await ExecuteSql(connectionString, sql, cancellationToken).ConfigureAwait(false);
    }

    public static async Task EnsureDatabaseExists(string connectionString, CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;

        var masterConnection = connectionString.Replace(builder.InitialCatalog, "master");

        using var connection = new SqlConnection(masterConnection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = $@"
if(db_id('{database}') is null)
    create database [{database}]
";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}