using PatternsLab.Domain.Abstractions;

namespace PatternsLab.Domain.Patients.Specifications;

/// <summary>
/// Note the name: not "GetAll". It is bounded, ordered and read-only by construction,
/// which is the difference between a specification and the <c>GetAllAsync</c> the
/// handbook calls "a production incident with a friendly name" (§3.3).
/// </summary>
public sealed class PatientDirectorySpec : Specification<Patient>
{
    public PatientDirectorySpec(string? nameContains = null, int page = 1, int pageSize = 50)
    {
        if (!string.IsNullOrWhiteSpace(nameContains))
        {
            var term = nameContains.Trim();
            Where(p => p.FullName.Contains(term));
        }

        ApplyOrderBy(p => p.FullName);
        ApplyPaging(page, pageSize);
        ReadOnly();
    }
}

public sealed class PatientByMemberNumberSpec : Specification<Patient>
{
    public PatientByMemberNumberSpec(string memberNumber)
    {
        var normalised = memberNumber.Trim().ToUpperInvariant();
        Where(p => p.MemberNumber == normalised);
    }
}
