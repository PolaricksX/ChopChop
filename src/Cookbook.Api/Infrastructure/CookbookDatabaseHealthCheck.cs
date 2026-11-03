using Cookbook.Data.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cookbook.Api.Infrastructure;

internal sealed class CookbookDatabaseHealthCheck(CookbookDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The Cookbook database is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The Cookbook database is unavailable.", exception);
        }
    }
}
