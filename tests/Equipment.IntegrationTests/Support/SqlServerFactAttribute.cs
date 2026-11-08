namespace Equipment.IntegrationTests.Support;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION")))
        {
            Skip = "Requires SQL Server; set TEST_SQLSERVER_CONNECTION to run this integration test.";
        }
    }
}
