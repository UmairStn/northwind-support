using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Health;

/// <summary>
/// Health check that verifies database reachability via EF Core (DK-7).
/// </summary>
public class DatabaseHealthCheck(SupportDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Cannot connect to database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection check failed.", ex);
        }
    }
}
