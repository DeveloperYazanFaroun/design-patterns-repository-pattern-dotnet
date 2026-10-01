using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatternsLab.Application.Abstractions;
using PatternsLab.Application.Claims;
using PatternsLab.Application.Claims.Handlers;
using PatternsLab.Application.Pricing;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;
using PatternsLab.Domain.Patients;
using PatternsLab.Infrastructure.Events;
using PatternsLab.Infrastructure.External;
using PatternsLab.Infrastructure.Persistence;
using PatternsLab.Infrastructure.Repositories;
using PatternsLab.Infrastructure.Repositories.Decorators;

namespace PatternsLab.Infrastructure.DependencyInjection;

/// <summary>
/// The composition root for everything below the API — handbook §3.8.
///
/// LIFETIMES ARE THE POINT OF THIS FILE. Table 3.1 in the handbook:
///
///   AppDbContext   scoped     one change tracker per request
///   Repository&lt;T&gt;  scoped     must match the context
///   IUnitOfWork    scoped     the unit IS the request
///   services       scoped     they depend on IUnitOfWork
///   audit trail    singleton  holds no scoped dependency — see the note in that class
///
/// Registering any of the scoped entries as a singleton produces the captive-dependency
/// bug, which is the single most common way a correct repository still fails in production.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration);
        services.AddRepositories();
        services.AddDomainEvents();
        services.AddApplicationServices();
        services.AddExternalServices();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = configuration.GetConnectionString("SqlServer")
                    ?? throw new InvalidOperationException(
                        "Database:Provider is SqlServer but ConnectionStrings:SqlServer is missing.");

                options.UseSqlServer(connectionString, sql =>
                {
                    // Retries transient failures — and is the reason UnitOfWork wraps its
                    // transaction in CreateExecutionStrategy (handbook §3.6).
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                    sql.CommandTimeout(30);
                });
            }
            else
            {
                var connectionString = configuration.GetConnectionString("Sqlite") ?? "Data Source=patternslab.db";
                options.UseSqlite(connectionString);
            }
        });   // scoped by default — exactly what we want

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Open generic: one registration satisfies IRepository<Claim>, IRepository<Patient>, …
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Aggregates that earned their own vocabulary.
        services.AddScoped<IClaimRepository, EfClaimRepository>();
        services.AddScoped<IPatientRepository, EfPatientRepository>();

        // DECORATORS, read outside-in. Registered last == outermost, so a call goes
        //   Logging -> Caching -> EfClaimRepository
        // Comment these two lines out and nothing else in the solution changes: that
        // substitutability is the concrete benefit the abstraction buys (handbook §4.1).
        services.Decorate<IClaimRepository, CachingClaimRepository>();
        services.Decorate<IClaimRepository, LoggingClaimRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    private static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Two observers of the same event; neither knows about the other.
        services.AddScoped<IDomainEventHandler<ClaimSubmitted>, NotifyBillingOnClaimSubmitted>();
        services.AddScoped<IDomainEventHandler<ClaimSubmitted>, RecordAuditOnClaimSubmitted>();
        services.AddScoped<IDomainEventHandler<ClaimRejected>, NotifyOnClaimRejected>();
        services.AddScoped<IDomainEventHandler<ClaimPaid>, RecordAuditOnClaimPaid>();

        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // STRATEGY: every rule registered against the same interface; PricingService
        // receives them as IEnumerable<IPricingRule> and picks by plan type.
        services.AddSingleton<IPricingRule>(_ => new FlatRatePricing());
        services.AddSingleton<IPricingRule>(_ => new TieredPricing());
        services.AddSingleton<IPricingRule>(_ => new ContractedRatePricing());
        services.AddSingleton<PricingService>();

        services.AddScoped<ClaimSubmissionService>();
        services.AddScoped<ClaimPaymentService>();
        services.AddScoped<ClaimQueryService>();
        services.AddScoped<ClaimSubmissionFacade>();

        return services;
    }

    private static IServiceCollection AddExternalServices(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddMemoryCache();

        services.AddSingleton<LegacySmtpGateway>();
        services.AddScoped<EmailNotifierAdapter>();
        services.AddScoped<SmsNotifier>();
        services.AddScoped<PushNotifier>();
        services.AddScoped<INotifierFactory, NotifierFactory>();

        services.AddScoped<IEligibilityChecker, StubEligibilityChecker>();
        services.AddScoped<IClearingHouseClient, StubClearingHouseClient>();

        // Singleton on purpose, and it holds NO scoped dependency. See the class comment.
        // Registered twice so the API can also read the concrete type's Entries property.
        services.AddSingleton<InMemoryAuditTrail>();
        services.AddSingleton<IAuditTrail>(sp => sp.GetRequiredService<InMemoryAuditTrail>());

        services.AddScoped<ICurrentUser, AnonymousUser>();

        return services;
    }
}
