using PatternsLab.Domain.Abstractions;

namespace PatternsLab.Domain.Patients;

public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByMemberNumberAsync(string memberNumber, CancellationToken ct = default);

    Task<bool> MemberNumberExistsAsync(string memberNumber, CancellationToken ct = default);
}
