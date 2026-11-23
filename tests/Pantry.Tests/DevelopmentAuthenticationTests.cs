using ChopChop.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Pantry.Tests;

public sealed class DevelopmentAuthenticationTests
{
    [Fact]
    public void Development_auth_mode_is_rejected_outside_development()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DevelopmentAuthentication.ConfigurationKey] = "true",
            })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DevelopmentAuthentication.AddIfEnabled(new ServiceCollection(), configuration, environment));

        Assert.Contains("only be enabled in the Development environment", exception.Message);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(DevelopmentAuthenticationTests);
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
