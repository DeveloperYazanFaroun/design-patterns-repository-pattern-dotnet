using Microsoft.EntityFrameworkCore;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Patients;
using PatternsLab.Infrastructure.Persistence;

namespace PatternsLab.Api;

/// <summary>
/// Seeds enough data that every endpoint returns something interesting on a fresh clone —
/// including a claim old enough to be "outstanding" and one that has been soft-deleted.
/// </summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var uow = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<IClock>();

        if (await db.Patients.AnyAsync(ct)) return;

        var patients = new[]
        {
            Patient.Register("Layla Haddad", "MEM-1001", new DateOnly(1988, 4, 12)),
            Patient.Register("Omar Nasser", "MEM-1002", new DateOnly(1975, 11, 3)),
            Patient.Register("Rana Khalil", "MEM-1003", new DateOnly(1994, 7, 26))
        };

        uow.Patients.AddRange(patients);

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // 1. Old and unpaid -> shows up in /api/claims/outstanding/PBM-01
        var outstanding = Claim.Create(patients[0].Id, "CLM-1001", today.AddDays(-60), "PBM-01");
        outstanding.AddLine("A4253", 100, new Money(0.42m));
        outstanding.AddLine("E0607", 1, new Money(38.00m));
        outstanding.Submit(now.AddDays(-58));

        // 2. Recent -> deliberately NOT outstanding, so the rule is visibly doing something
        var recent = Claim.Create(patients[1].Id, "CLM-1002", today.AddDays(-4), "PBM-01");
        recent.AddLine("J1815", 30, new Money(6.25m));
        recent.Submit(now.AddDays(-3));

        // 3. Paid -> excluded by the rule for a different reason
        var paid = Claim.Create(patients[1].Id, "CLM-1003", today.AddDays(-90), "PBM-01");
        paid.AddLine("A4253", 250, new Money(0.42m));
        paid.Submit(now.AddDays(-88));
        paid.MarkPaid(now.AddDays(-40));

        // 4. A different payer -> proves the filter is not cosmetic
        var otherPayer = Claim.Create(patients[2].Id, "CLM-1004", today.AddDays(-45), "PBM-07");
        otherPayer.AddLine("E0607", 2, new Money(38.00m));
        otherPayer.Submit(now.AddDays(-44));

        // 5. Still a draft
        var draft = Claim.Create(patients[2].Id, "CLM-1005", today.AddDays(-1), "PBM-01");
        draft.AddLine("A4253", 10, new Money(0.42m));

        uow.Claims.AddRange([outstanding, recent, paid, otherPayer, draft]);

        patients[0].RecordSubmission(now);
        patients[1].RecordSubmission(now);
        patients[2].RecordSubmission(now);

        await uow.SaveChangesAsync(ct);

        // 6. One soft-deleted claim, so /api/claims/deleted is not empty and the global
        //    query filter can be seen hiding it from every other endpoint.
        var toDelete = Claim.Create(patients[0].Id, "CLM-1006", today.AddDays(-30), "PBM-01");
        toDelete.AddLine("J1815", 5, new Money(6.25m));
        uow.Claims.Add(toDelete);
        await uow.SaveChangesAsync(ct);

        uow.Claims.Remove(toDelete);            // becomes IsDeleted = true inside SaveChangesAsync
        await uow.SaveChangesAsync(ct);
    }
}
