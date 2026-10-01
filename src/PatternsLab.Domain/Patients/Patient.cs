using PatternsLab.Domain.Common;

namespace PatternsLab.Domain.Patients;

/// <summary>
/// Second aggregate root. It exists so the sample has a use case that changes
/// <em>two</em> aggregates and must commit them together — which is the whole
/// reason the Unit of Work is there (handbook §3.7, Figure 3.2).
/// </summary>
public sealed class Patient : AggregateRoot, IAuditable, ISoftDeletable, IHasConcurrencyToken
{
    private Patient() { }

    private Patient(string fullName, string memberNumber, DateOnly dateOfBirth)
    {
        FullName = fullName;
        MemberNumber = memberNumber;
        DateOfBirth = dateOfBirth;
    }

    public string FullName { get; private set; } = string.Empty;
    public string MemberNumber { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }

    public int SubmittedClaimCount { get; private set; }
    public DateTimeOffset? LastSubmissionOn { get; private set; }

    public DateTimeOffset CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedOn { get; set; }
    public byte[]? RowVersion { get; set; }

    public static Patient Register(string fullName, string memberNumber, DateOnly dateOfBirth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberNumber);

        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));

        return new Patient(fullName.Trim(), memberNumber.Trim().ToUpperInvariant(), dateOfBirth);
    }

    public void Rename(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        FullName = fullName.Trim();
    }

    /// <summary>Called in the same transaction as the claim's own Submit().</summary>
    public void RecordSubmission(DateTimeOffset now)
    {
        SubmittedClaimCount++;
        LastSubmissionOn = now;
    }
}
