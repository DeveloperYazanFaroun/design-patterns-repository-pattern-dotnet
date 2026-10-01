using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims.Specifications;

namespace PatternsLab.Api.Workers;

/// <summary>
/// THE CAPTIVE-DEPENDENCY LESSON, in runnable form — handbook §1.4 and Table 3.1.
///
/// A <c>BackgroundService</c> is a SINGLETON. Writing
/// <code>public OutstandingClaimChaser(IUnitOfWork uow)</code>
/// compiles, starts, and then fails in production: the scoped unit of work (and the
/// DbContext inside it) is captured for the lifetime of the process, is not thread-safe,
/// and accumulates every entity it has ever tracked.
///
/// The fix is this class: inject <see cref="IServiceScopeFactory"/> and open one scope
/// per unit of work.
///
/// It is also a Template Method (§1.6): the base class owns the start/stop/cancellation
/// skeleton and calls the one step you supply.
/// </summary>
public sealed class OutstandingClaimChaser(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OutstandingClaimChaser> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = configuration.GetValue("Workers:ClaimChaser:Enabled", defaultValue: true);
        var interval = TimeSpan.FromMinutes(configuration.GetValue("Workers:ClaimChaser:IntervalMinutes", 30));
        var payerCode = configuration["Workers:ClaimChaser:PayerCode"] ?? "PBM-01";

        if (!enabled)
        {
            logger.LogInformation("Claim chaser is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                // ONE SCOPE PER RUN. This is the whole point of the class.
                await using var scope = scopeFactory.CreateAsyncScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                // tracked: true — we are about to MODIFY these. The read-only variant used by
                // the API would let FlagForChase() run and then quietly save nothing.
                var overdue = await uow.Claims.ListAsync(
                    new OutstandingClaimsSpec(payerCode, clock.UtcNow, pageSize: 500, tracked: true),
                    stoppingToken);

                foreach (var claim in overdue.Where(c => !c.IsFlaggedForChase))
                    claim.FlagForChase();

                var written = await uow.SaveChangesAsync(stoppingToken);

                logger.LogInformation(
                    "Claim chase run for {Payer}: {Found} outstanding, {Written} row(s) updated.",
                    payerCode, overdue.Count, written);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;   // shutting down — not an error
            }
            catch (Exception ex)
            {
                // A worker that throws stops forever. Log and live to run again.
                logger.LogError(ex, "Claim chase run failed. The next run will retry.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
