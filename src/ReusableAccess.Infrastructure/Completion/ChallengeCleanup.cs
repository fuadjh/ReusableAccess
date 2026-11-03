using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Completion;

public sealed class ChallengeCleanup(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<ChallengeCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                var cutoff = clock.GetUtcNow().AddDays(-1);

                await db.Set<AccountChallenge>()
                    .Where(x => x.ExpiresUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                logger.LogWarning("Account challenge cleanup failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}