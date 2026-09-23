namespace Flicked.Api.Services;

/* Runs ServerPool.SweepAsync on a timer: expired leases back into the pool,
   silent servers marked offline.

   A BackgroundService is a singleton and lives as long as the app, while a
   DbContext is scoped to a request and must not be shared or kept. So each pass
   opens its own scope, exactly as a request would. Holding one context here for
   the life of the process is the standard way to leak memory and see stale data
   in a .NET app. */
public class PoolJanitor(IServiceScopeFactory scopes, ILogger<PoolJanitor> log) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(Every);

        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var pool = scope.ServiceProvider.GetRequiredService<ServerPool>();
                await pool.SweepAsync(stopping);
                // and ask each free server whether it is still there
                await pool.CheckAsync(stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                break;   // shutting down, not a failure
            }
            catch (Exception e)
            {
                /* Swallowed on purpose. An unhandled exception here would end the
                   loop for the life of the process, and the pool would silently
                   stop being tidied. The database being briefly unreachable
                   should cost one pass, not all of them. */
                log.LogError(e, "Pool sweep failed; carrying on");
            }

            try { await timer.WaitForNextTickAsync(stopping); }
            catch (OperationCanceledException) { break; }
        }
    }
}
