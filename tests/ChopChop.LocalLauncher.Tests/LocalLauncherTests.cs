using System.Data.Common;

namespace ChopChop.LocalLauncher.Tests;

public sealed class LocalLauncherTests
{
    [Fact]
    public void ApiTargetsUseComposeResolvedPassword()
    {
        const string configuration = """
            {"services":{"sqlserver":{"environment":{"MSSQL_SA_PASSWORD":"Strong$$Password123!"}}}}
            """;
        var password = global::LocalLauncher.ReadComposePassword(configuration);
        var target = global::LocalLauncher.CreateTargets("repository", password)
            .First(target => target.FileName == "dotnet");
        var connectionString = new DbConnectionStringBuilder
        {
            ConnectionString = target.Environment["ConnectionStrings__ChopChop"]
        };

        Assert.Equal("Strong$Password123!", connectionString["Password"]);
        Assert.Equal("amqp://chopchop_test:Strong%24Password123%21@127.0.0.1:5673/", target.Environment["RabbitMq__Uri"]);
    }

    [Fact]
    public void ApiTargetsUseIsolatedSqlServerPort()
    {
        var targets = global::LocalLauncher.CreateTargets("repository", "test-password");
        var apiTargets = targets.Where(target => target.FileName == "dotnet").ToList();

        Assert.NotEmpty(apiTargets);
        foreach (var target in apiTargets)
        {
            foreach (var key in new[] { "ConnectionStrings__ChopChop", "ConnectionStrings__Equipment", "ConnectionStrings__Pantry" })
            {
                var connectionString = new DbConnectionStringBuilder { ConnectionString = target.Environment[key] };
                Assert.Equal("127.0.0.1,1435", connectionString["Server"]);
            }
        }
    }
}
