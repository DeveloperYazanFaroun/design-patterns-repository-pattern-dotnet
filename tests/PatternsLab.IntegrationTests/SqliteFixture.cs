using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PatternsLab.Application.Abstractions;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;
using PatternsLab.Domain.Patients;
using PatternsLab.Infrastructure.Persistence;
using PatternsLab.Infrastructure.Repositories;

namespace PatternsLab.IntegrationTests;

/// <summary>
/// SQLite in-memory — handbook §3.11.
///
/// Deliberately NOT Microsoft.EntityFrameworkCore.InMemory: that provider has no joins,
/// no constraints and no transactions, so queries that pass against it routinely fail
/// against SQL Server. SQLite is a real relational engine, which is the whole point of
/// an integration test.
///
/// The connection is kept OPEN for the fixture's lifetime; close it and an in-memory
/// SQLite database ceases to exist.
/// </summary>
public sealed class SqliteFixture : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public AppDbContext Db { get; private set; } = null!;
    public IUnitOfWork Uow { get; private set; } = null!;
    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        Db = new AppDbContext(options);
        await Db.Database.EnsureCreatedAsync();

        Uow = BuildUnitOfWork(Db);
    }

    /// <summary>A second unit of work over the SAME database — used to simulate a second user.</summary>
    public (AppDbContext Db, IUnitOfWork Uow) NewScope()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        return (db, BuildUnitOfWork(db));
    }

    private IUnitOfWork BuildUnitOfWork(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped<IClaimRepository>(_ => new EfClaimRepository(db, Clock));
        services.AddScoped<IPatientRepository>(_ => new EfPatientRepository(db));
        services.AddSingleton<ICurrentUser>(_ =>
        {
            var user = Substitute.For<ICurrentUser>();
            user.UserId.Returns("test-user");
            return user;
        });

        var provider = services.BuildServiceProvider();

        return new UnitOfWork(db, provider, new NoOpDispatcher(), Clock, NullLogger<UnitOfWork>.Instance);
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        public void Advance(TimeSpan by) => UtcNow += by;
    }

    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Dispatched { get; } = [];

        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default)
        {
            Dispatched.AddRange(events);
            return Task.CompletedTask;
        }
    }

    // ---- helpers the tests share ------------------------------------------

    public Patient AddPatient(string name = "Layla Haddad", string memberNumber = "MEM-1001")
    {
        var patient = Patient.Register(name, memberNumber, new DateOnly(1988, 4, 12));
        Db.Patients.Add(patient);
        Db.SaveChanges();
        return patient;
    }

    public Claim AddSubmittedClaim(
        Guid patientId, string number, string payer = "PBM-01", int submittedDaysAgo = 45)
    {
        var claim = Claim.Create(patientId, number,
            DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime.AddDays(-submittedDaysAgo - 1)), payer);

        claim.AddLine("A4253", 100, new Money(0.42m));
        claim.Submit(Clock.UtcNow.AddDays(-submittedDaysAgo));

        Db.Claims.Add(claim);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();

        return claim;
    }
}
