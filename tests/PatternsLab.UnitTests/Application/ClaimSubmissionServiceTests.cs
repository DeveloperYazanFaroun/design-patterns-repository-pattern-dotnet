using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PatternsLab.Application.Abstractions;
using PatternsLab.Application.Claims;
using PatternsLab.Application.Pricing;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;
using PatternsLab.Domain.Patients;
using Shouldly;

namespace PatternsLab.UnitTests.Application;

/// <summary>
/// THE POINT OF THE ABSTRACTION, demonstrated — handbook §3.11, §4.1.
///
/// Every test here runs with no database, no EF Core and no I/O, because the service
/// depends on <see cref="IUnitOfWork"/> rather than on a DbContext. They finish in
/// milliseconds, which is why they get run rather than skipped.
/// </summary>
public class ClaimSubmissionServiceTests
{
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly IPatientRepository _patients = Substitute.For<IPatientRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEligibilityChecker _eligibility = Substitute.For<IEligibilityChecker>();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private ClaimSubmissionService CreateSut()
    {
        _uow.Claims.Returns(_claims);
        _uow.Patients.Returns(_patients);

        // ExecuteInTransactionAsync just runs the delegate in these tests — we are asserting
        // the service's decisions, not the database's transaction semantics.
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<Guid>>>>()(CancellationToken.None));

        _eligibility.VerifyAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                    .Returns(new EligibilityResult(true, null, "contracted"));

        var pricing = new PricingService([new FlatRatePricing(), new TieredPricing(), new ContractedRatePricing()]);

        return new ClaimSubmissionService(_uow, pricing, _eligibility, _clock,
            NullLogger<ClaimSubmissionService>.Instance);
    }

    private static SubmitClaimCommand ValidCommand(Guid patientId) =>
        new(patientId, "CLM-001", new DateOnly(2026, 5, 20), "PBM-01",
            [new ClaimLineInput("A4253", 100), new ClaimLineInput("E0607", 1)]);

    [Fact]
    public async Task Returns_conflict_when_the_claim_number_already_exists()
    {
        var sut = CreateSut();
        _claims.NumberExistsAsync("CLM-001", Arg.Any<CancellationToken>()).Returns(true);

        var result = await sut.SubmitAsync(ValidCommand(Guid.CreateVersion7()));

        result.Status.ShouldBe(ResultStatus.Conflict);
        result.Error.ShouldContain("CLM-001");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_not_found_when_the_patient_does_not_exist()
    {
        var sut = CreateSut();
        _claims.NumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _patients.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Patient?)null);

        var result = await sut.SubmitAsync(ValidCommand(Guid.CreateVersion7()));

        result.Status.ShouldBe(ResultStatus.NotFound);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_invalid_when_the_command_has_no_lines()
    {
        var sut = CreateSut();

        var result = await sut.SubmitAsync(
            new SubmitClaimCommand(Guid.CreateVersion7(), "CLM-001", new DateOnly(2026, 5, 20), "PBM-01", []));

        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public async Task Returns_invalid_when_the_patient_is_not_eligible()
    {
        var sut = CreateSut();
        var patient = Patient.Register("Layla Haddad", "MEM-1001", new DateOnly(1988, 4, 12));

        _claims.NumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _patients.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _eligibility.VerifyAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                    .Returns(new EligibilityResult(false, "Coverage lapsed", null));

        var result = await sut.SubmitAsync(ValidCommand(patient.Id));

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.Error.ShouldContain("Coverage lapsed");
    }

    [Fact]
    public async Task Happy_path_adds_the_claim_updates_the_patient_and_commits_exactly_once()
    {
        var sut = CreateSut();
        var patient = Patient.Register("Layla Haddad", "MEM-1001", new DateOnly(1988, 4, 12));

        _claims.NumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _patients.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);

        var result = await sut.SubmitAsync(ValidCommand(patient.Id));

        result.IsSuccess.ShouldBeTrue();

        // BOTH aggregates changed…
        _claims.Received(1).Add(Arg.Is<Claim>(c => c.Number == "CLM-001" && c.Status == ClaimStatus.Submitted));
        patient.SubmittedClaimCount.ShouldBe(1);

        // …and there was exactly ONE commit. That is the Unit of Work doing its job.
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Happy_path_prices_the_lines_with_the_strategy_the_eligibility_check_selected()
    {
        var sut = CreateSut();
        var patient = Patient.Register("Omar Nasser", "MEM-1002", new DateOnly(1975, 11, 3));

        _claims.NumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _patients.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);

        Claim? added = null;
        _claims.When(r => r.Add(Arg.Any<Claim>())).Do(call => added = call.Arg<Claim>());

        await sut.SubmitAsync(ValidCommand(patient.Id));

        // "contracted" rates: A4253 = 0.42, E0607 = 38.00  ->  100*0.42 + 1*38 = 80.00
        added.ShouldNotBeNull();
        added!.Total.Amount.ShouldBe(80.00m);
    }
}
