using Microsoft.EntityFrameworkCore;
using PatternsLab.Domain.Patients;
using PatternsLab.Infrastructure.Persistence;

namespace PatternsLab.Infrastructure.Repositories;

public sealed class EfPatientRepository(AppDbContext db) : Repository<Patient>(db), IPatientRepository
{
    public async Task<Patient?> GetByMemberNumberAsync(string memberNumber, CancellationToken ct = default)
    {
        var normalised = memberNumber.Trim().ToUpperInvariant();
        return await Set.FirstOrDefaultAsync(p => p.MemberNumber == normalised, ct);
    }

    public async Task<bool> MemberNumberExistsAsync(string memberNumber, CancellationToken ct = default)
    {
        var normalised = memberNumber.Trim().ToUpperInvariant();
        return await Set.IgnoreQueryFilters().AnyAsync(p => p.MemberNumber == normalised, ct);
    }
}
