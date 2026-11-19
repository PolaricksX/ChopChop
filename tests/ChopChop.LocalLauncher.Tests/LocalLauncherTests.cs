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
        var target = global::LocalLauncher.CreateTargets("repository", password, localAuth: true)
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
        var targets = global::LocalLauncher.CreateTargets("repository", "test-password", localAuth: true);
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

    [Fact]
    public void LocalAuthModeEnablesDevelopmentAuthenticationForApisAndClient()
    {
        var targets = global::LocalLauncher.CreateTargets("repository", "test-password", localAuth: true);
        var apiTargets = targets.Where(target => target.FileName == "dotnet");
        var client = Assert.Single(targets, target => target.FileName == "node");

        Assert.All(apiTargets, target => Assert.Equal("true", target.Environment["Auth__DevelopmentMode"]));
        Assert.Equal("true", client.Environment["VITE_DEV_AUTH"]);
    }

    [Fact]
    public void Auth0ModeConfiguresAuthorityAudiencesAndDisablesDevelopmentAuthentication()
    {
        const string authority = "https://tenant.example/";
        var targets = global::LocalLauncher.CreateTargets("repository", "test-password", auth0Authority: authority);
        var apiTargets = targets.Where(target => target.FileName == "dotnet").ToList();
        var client = Assert.Single(targets, target => target.FileName == "node");
        var expectedAudiences = new[]
        {
            "https://api.chopchop.local/ai-recipe-generation",
            "https://api.chopchop.local/interactive-review",
            "https://api.chopchop.local/recipe-management",
            "https://api.chopchop.local/cookbook",
            "https://api.chopchop.local/equipment",
            "https://api.chopchop.local/pantry",
            "https://api.chopchop.local/meal-planner",
            "https://api.chopchop.local/user-service"
        };

        Assert.Equal(8, apiTargets.Count);
        Assert.All(apiTargets, target =>
        {
            Assert.Equal("false", target.Environment["Auth__DevelopmentMode"]);
            Assert.Equal(authority, target.Environment["Auth__Authority"]);
            Assert.Contains(target.Environment["Auth__Audience"], expectedAudiences);
        });
        Assert.Equal(expectedAudiences.Order(), apiTargets.Select(target => target.Environment["Auth__Audience"]).Order());
        Assert.Equal("false", client.Environment["VITE_DEV_AUTH"]);
    }
}
