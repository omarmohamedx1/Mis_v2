using MIS.Infrastructure.Services;
namespace MIS.API.Configuration;
public sealed class HrMissionNotificationWorker(IServiceScopeFactory scopes, ILogger<HrMissionNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<HrMissionSynchronizer>().SyncAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "HR mission notification synchronization failed; will retry."); }
            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
