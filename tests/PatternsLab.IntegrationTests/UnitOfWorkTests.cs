using Microsoft.EntityFrameworkCore;
using PatternsLab.Application.Claims;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Claims.Specifications;
using Shouldly;

namespace PatternsLab.IntegrationTests;

/// <summary>
/// The four things the Unit of Work promises, verified against a real relational engine:
/// atomicity, audit stamping, soft delete and optimistic concurrency.
/// </summary>
public class UnitOfWorkTests(SqliteFixture fx) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task Two_aggregates_are_committed_together()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);

        var claim = Claim.Create(patient.Id, $"UOW-{Guid.NewGuid():N}"[..12],
            DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime.AddDays(-2)), "PBM-01");
        claim.AddLine("A4253", 10, new Money(1m));
        claim.Submit(fx.Clock.UtcNow);

        var tracked = await fx.Uow.Patients.GetByIdAsync(patient.Id);
        tracked!.RecordSubmission(fx.Clock.UtcNow);

        fx.Uow.Claims.Add(claim);
        await fx.Uow.SaveChangesAsync();

        fx.Db.ChangeTracker.Clear();

        (await fx.Uow.Claims.GetByIdAsync(claim.Id)).ShouldNotBeNull();
        (await fx.Uow.Patients.GetByIdAsync(patient.Id))!.SubmittedClaimCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_failure_inside_the_transaction_rolls_everything_back()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
        var number = $"RB-{Guid.NewGuid():N}"[..12];

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await fx.Uow.ExecuteInTransactionAsync(async ct =>
            {
                var claim = Claim.Create(patient.Id, number,
                    DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime.AddDays(-2)), "PBM-01");
                claim.AddLine("A4253", 10, new Money(1m));
                claim.Submit(fx.Clock.UtcNow);

                fx.Uow.Claims.Add(claim);
                await fx.Uow.SaveChangesAsync(ct);

                throw new InvalidOperationException("Simulated failure AFTER the first save.");
            }));

        fx.Db.ChangeTracker.Clear();

        // The save inside the transaction was undone with it. Nothing was written.
        (await fx.Uow.Claims.GetByNumberAsync(number)).ShouldBeNull();
    }

    [Fact]
    public async Task Audit_fields_are_stamped_without_any_service_asking()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);

        var claim = Claim.Create(patient.Id, $"AUD-{Guid.NewGuid():N}"[..12],
            DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime.AddDays(-2)), "PBM-01");
        claim.AddLine("A4253", 10, new Money(1m));

        fx.Uow.Claims.Add(claim);
        await fx.Uow.SaveChangesAsync();

        claim.CreatedOn.ShouldBe(fx.Clock.UtcNow);
        claim.CreatedBy.ShouldBe("test-user");
        claim.ModifiedOn.ShouldBeNull();

        // Now modify it and confirm CreatedOn is not overwritten.
        var createdOn = claim.CreatedOn;
        fx.Clock.Advance(TimeSpan.FromHours(1));

        claim.Submit(fx.Clock.UtcNow);
        await fx.Uow.SaveChangesAsync();

        claim.CreatedOn.ShouldBe(createdOn);
        claim.ModifiedOn.ShouldBe(fx.Clock.UtcNow);
    }

    [Fact]
    public async Task Remove_becomes_a_soft_delete_and_the_row_survives()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
        var claim = fx.AddSubmittedClaim(patient.Id, $"SD-{Guid.NewGuid():N}"[..12], "PBM-SD", 20);

        var tracked = await fx.Uow.Claims.GetByIdAsync(claim.Id);
        fx.Uow.Claims.Remove(tracked!);
        await fx.Uow.SaveChangesAsync();

        fx.Db.ChangeTracker.Clear();

        // Invisible through the global query filter…
        (await fx.Uow.Claims.GetByIdAsync(claim.Id)).ShouldBeNull();

        // …but still in the table, with its tombstone fields set.
        var raw = await fx.Db.Claims.IgnoreQueryFilters().SingleAsync(c => c.Id == claim.Id);
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedOn.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_specification_can_opt_out_of_the_soft_delete_filter()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
        var claim = fx.AddSubmittedClaim(patient.Id, $"OPT-{Guid.NewGuid():N}"[..12], "PBM-OPT", 20);

        var tracked = await fx.Uow.Claims.GetByIdAsync(claim.Id);
        fx.Uow.Claims.Remove(tracked!);
        await fx.Uow.SaveChangesAsync();
        fx.Db.ChangeTracker.Clear();

        var deleted = await fx.Uow.Claims.ListAsync(new DeletedClaimsSpec());

        deleted.Select(c => c.Id).ShouldContain(claim.Id);
    }

    [Fact]
    public async Task A_second_editor_loses_and_is_told_why()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
        var claim = fx.AddSubmittedClaim(patient.Id, $"CC-{Guid.NewGuid():N}"[..12], "PBM-CC", 20);

        // Two users load the same claim through two different contexts.
        var (dbA, uowA) = fx.NewScope();
        var (dbB, uowB) = fx.NewScope();

        var copyA = await uowA.Claims.GetByIdAsync(claim.Id);
        var copyB = await uowB.Claims.GetByIdAsync(claim.Id);

        copyA!.MarkPaid(fx.Clock.UtcNow);
        await uowA.SaveChangesAsync();          // first writer wins

        copyB!.Accept();

        // Second writer is refused rather than silently overwriting.
        await Should.ThrowAsync<ConcurrencyConflictException>(() => uowB.SaveChangesAsync());

        await dbA.DisposeAsync();
        await dbB.DisposeAsync();
    }

    [Fact]
    public async Task Domain_events_are_cleared_after_a_successful_save()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);

        var claim = Claim.Create(patient.Id, $"EV-{Guid.NewGuid():N}"[..12],
            DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime.AddDays(-2)), "PBM-01");
        claim.AddLine("A4253", 10, new Money(1m));
        claim.Submit(fx.Clock.UtcNow);

        claim.DomainEvents.Count.ShouldBe(1);

        fx.Uow.Claims.Add(claim);
        await fx.Uow.SaveChangesAsync();

        // Drained by the Unit of Work, so a second save cannot re-publish them.
        claim.DomainEvents.ShouldBeEmpty();
    }
}
