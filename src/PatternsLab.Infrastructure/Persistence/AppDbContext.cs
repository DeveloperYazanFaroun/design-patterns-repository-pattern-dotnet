using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;
using PatternsLab.Domain.Patients;

namespace PatternsLab.Infrastructure.Persistence;

/// <summary>
/// The Data Mapper (handbook §2.6). EF Core already IS one — the repository sits on
/// top of it rather than replacing it.
///
/// Registered as <b>scoped</b>, which is what gives every repository in one request the
/// same change tracker. Break that and the Unit of Work stops being a unit (§3.1).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<Patient> Patients => Set<Patient>();

    /// <summary>
    /// SQLite has no date type: a <see cref="DateTimeOffset"/> lands in a TEXT column, and the
    /// provider refuses to translate a comparison or an ORDER BY over one. That would break every
    /// specification that filters or sorts on <c>SubmittedOn</c> — including OutstandingClaimsSpec.
    ///
    /// Storing the value as a sortable binary long fixes both. Same conditional-on-provider trick
    /// as <see cref="ApplyConcurrencyTokens"/>: SQL Server needs none of this, and the domain
    /// still knows nothing about either.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        if (!Database.IsSqlServer())
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();

        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplySoftDeleteFilter(modelBuilder);
        ApplyConcurrencyTokens(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Optimistic concurrency, handbook §3.10.
    ///
    /// SQL Server maintains a <c>rowversion</c> column itself. SQLite has no equivalent,
    /// so there the token is an ordinary column that <see cref="UnitOfWork"/> stamps on
    /// every save. The domain does not know the difference — which is the point.
    /// </summary>
    private void ApplyConcurrencyTokens(ModelBuilder modelBuilder)
    {
        var storeGenerated = Database.IsSqlServer();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IHasConcurrencyToken).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.IsOwned()) continue;

            var property = modelBuilder.Entity(entityType.ClrType)
                                       .Property(nameof(IHasConcurrencyToken.RowVersion));

            if (storeGenerated)
            {
                property.IsRowVersion();
            }
            else
            {
                property.IsConcurrencyToken().ValueGeneratedNever();
            }
        }
    }

    /// <summary>
    /// Soft delete written once, for every entity that opts in, instead of a
    /// "&amp;&amp; !c.IsDeleted" repeated in forty queries (handbook §3.2, §3.12).
    /// </summary>
    private static void ApplySoftDeleteFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;

            // Owned types inherit their owner's filter; setting one on them throws.
            if (entityType.IsOwned()) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(property), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
