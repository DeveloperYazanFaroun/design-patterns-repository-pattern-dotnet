using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatternsLab.Domain.Patients;

namespace PatternsLab.Infrastructure.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FullName).HasMaxLength(200).IsRequired();

        builder.Property(p => p.MemberNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => p.MemberNumber).IsUnique().HasDatabaseName("UX_Patients_MemberNumber");

        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(p => p.IsDeleted).HasDatabaseName("IX_Patients_IsDeleted");

        builder.Ignore(p => p.DomainEvents);
    }
}
