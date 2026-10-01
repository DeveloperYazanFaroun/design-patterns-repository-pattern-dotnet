using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatternsLab.Domain.Claims;

namespace PatternsLab.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping lives here, not in the entity. The domain has no idea it is stored in a
/// relational database — that is what keeps <c>PatternsLab.Domain</c> free of an
/// EF Core reference (handbook §2.5).
/// </summary>
public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Number).HasMaxLength(32).IsRequired();
        builder.HasIndex(c => c.Number).IsUnique().HasDatabaseName("UX_Claims_Number");

        builder.Property(c => c.PayerCode).HasMaxLength(16).IsRequired();
        builder.Property(c => c.RejectionReason).HasMaxLength(512);
        builder.Property(c => c.Status).HasConversion<int>();

        // Covers the OutstandingClaimsSpec query — see docs/EXERCISES.md, Exercise 6.
        builder.HasIndex(c => new { c.PayerCode, c.Status, c.SubmittedOn })
               .HasDatabaseName("IX_Claims_Payer_Status_SubmittedOn");

        // Value object stored inline on the Claims table.
        builder.OwnsOne(c => c.Total, money =>
        {
            money.Property(m => m.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("TotalCurrency").HasMaxLength(3);
        });

        // Lines are part of the aggregate: no DbSet, no repository, cascade delete.
        builder.OwnsMany(c => c.Lines, line =>
        {
            line.ToTable("ClaimLines");
            line.WithOwner().HasForeignKey("ClaimId");
            line.HasKey(l => l.Id);

            line.Property(l => l.ProcedureCode).HasMaxLength(16).IsRequired();
            line.Property(l => l.Quantity).IsRequired();

            line.OwnsOne(l => l.UnitPrice, money =>
            {
                money.Property(m => m.Amount).HasColumnName("UnitPriceAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3);
            });
        });

        // Lines is exposed as IReadOnlyList with no setter, so EF must write through the
        // private _lines field. This is the standard DDD mapping idiom.
        builder.Metadata.FindNavigation(nameof(Claim.Lines))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(c => c.Patient)
               .WithMany()
               .HasForeignKey(c => c.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.CreatedBy).HasMaxLength(128);
        builder.Property(c => c.ModifiedBy).HasMaxLength(128);
        builder.HasIndex(c => c.IsDeleted).HasDatabaseName("IX_Claims_IsDeleted");

        // The concurrency token is configured centrally in AppDbContext, because how it
        // is generated depends on the provider (SQL Server rowversion vs. SQLite).

        // Domain events are in-memory only — never a column.
        builder.Ignore(c => c.DomainEvents);
    }
}
