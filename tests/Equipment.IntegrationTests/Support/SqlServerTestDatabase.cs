using Equipment.Data.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Equipment.IntegrationTests.Support;

public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const string DatabasePrefix = "EquipmentTests_";
    private readonly string masterConnectionString;

    private SqlServerTestDatabase(string connectionString, string masterConnectionString, string databaseName)
    {
        ConnectionString = connectionString;
        this.masterConnectionString = masterConnectionString;
        DatabaseName = databaseName;
    }

    public string ConnectionString { get; }

    private string DatabaseName { get; }

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var source = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException(
                "SQL Server tests require the TEST_SQLSERVER_CONNECTION environment variable.");
        }

        var databaseName = $"{DatabasePrefix}{Guid.NewGuid():N}";
        var databaseBuilder = new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = databaseName,
        };
        var masterBuilder = new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = "master",
        };
        var database = new SqlServerTestDatabase(
            databaseBuilder.ConnectionString,
            masterBuilder.ConnectionString,
            databaseName);

        await database.CreateDatabaseAsync();
        try
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DropDatabaseAsync();
            throw;
        }
    }

    public EquipmentDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<EquipmentDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    public async ValueTask DisposeAsync() => await DropDatabaseAsync();

    private async Task CreateDatabaseAsync()
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
                              EXEC(@sql);
                              """;
        command.Parameters.Add(new SqlParameter("@databaseName", DatabaseName));
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        if (!DatabaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to drop unexpected database '{DatabaseName}'.");
        }

        using (var pooledConnection = new SqlConnection(ConnectionString))
        {
            SqlConnection.ClearPool(pooledConnection);
        }
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              IF DB_ID(@databaseName) IS NOT NULL
                              BEGIN
                                  DECLARE @setSingleUserSql nvarchar(max) =
                                      N'ALTER DATABASE ' + QUOTENAME(@databaseName) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE';
                                  DECLARE @dropSql nvarchar(max) =
                                      N'DROP DATABASE ' + QUOTENAME(@databaseName);
                                  EXEC(@setSingleUserSql);
                                  EXEC(@dropSql);
                              END
                              """;
        command.Parameters.Add(new SqlParameter("@databaseName", DatabaseName));
        await command.ExecuteNonQueryAsync();
    }
}
