using PatternsLab.Domain.Common;

namespace PatternsLab.Domain.Claims;

public enum ClaimStatus
{
    Draft = 0,
    Submitted = 1,
    Accepted = 2,
    Rejected = 3,
    Paid = 4,
    Voided = 5
}

/// <summary>
/// Aggregate root for a billing claim.
///
/// Everything that changes a claim goes through a method on this class. There are no
/// public setters, so no service can put a claim into a state the domain forbids —
/// that is the difference between a rich aggregate and a bag of properties (§4.1).
/// </summary>
public sealed class Claim : AggregateRoot, IAuditable, ISoftDeletable, IHasConcurrencyToken
{
    private readonly List<ClaimLine> _lines = [];

    // EF Core needs a parameterless ctor it can call; keep it private so nothing else uses it.
    private Claim() { }

    private Claim(Guid patientId, string number, DateOnly serviceDate, string payerCode)
    {
        PatientId = patientId;
        Number = number;
        ServiceDate = serviceDate;
        PayerCode = payerCode;
        Status = ClaimStatus.Draft;
    }

    public Guid PatientId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public DateOnly ServiceDate { get; private set; }
    public string PayerCode { get; private set; } = string.Empty;
    public ClaimStatus Status { get; private set; }
    public DateTimeOffset? SubmittedOn { get; private set; }
    public DateTimeOffset? PaidOn { get; private set; }
    public string? RejectionReason { get; private set; }
    public bool IsFlaggedForChase { get; private set; }

    public Money Total { get; private set; } = Money.Zero();

    public IReadOnlyList<ClaimLine> Lines => _lines.AsReadOnly();

    // Navigation for the Include() in the repository. Never dereferenced by domain logic.
    public Patients.Patient? Patient { get; private set; }

    // -- infrastructure concerns, populated by the Unit of Work --------------
    public DateTimeOffset CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedOn { get; set; }
    public byte[]? RowVersion { get; set; }

    // -- factory --------------------------------------------------------------

    /// <summary>
    /// Factory Method (handbook §1.4): construction is a named operation with rules,
    /// not a bare <c>new</c> that anyone can misuse.
    /// </summary>
    public static Claim Create(Guid patientId, string number, DateOnly serviceDate, string payerCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(payerCode);

        if (patientId == Guid.Empty)
            throw new ArgumentException("A claim must belong to a patient.", nameof(patientId));

        if (serviceDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Service date cannot be in the future.", nameof(serviceDate));

        return new Claim(patientId, number.Trim().ToUpperInvariant(), serviceDate, payerCode.Trim().ToUpperInvariant());
    }

    // -- behaviour -------------------------------------------------------------

    public void AddLine(string procedureCode, int quantity, Money unitPrice)
    {
        EnsureDraft("add a line to");

        ArgumentException.ThrowIfNullOrWhiteSpace(procedureCode);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var code = procedureCode.Trim().ToUpperInvariant();

        if (_lines.Any(l => l.ProcedureCode == code))
        {
            throw new InvalidOperationException(
                $"Claim {Number} already has a line for procedure code '{code}'. " +
                "Adjust the quantity on the existing line instead of adding a duplicate.");
        }

        _lines.Add(ClaimLine.Create(code, quantity, unitPrice));
        Recalculate();
    }

    public void RemoveLine(string procedureCode)
    {
        EnsureDraft("remove a line from");

        var code = procedureCode.Trim().ToUpperInvariant();
        var line = _lines.SingleOrDefault(l => l.ProcedureCode == code)
                   ?? throw new InvalidOperationException($"Claim {Number} has no line for '{code}'.");

        _lines.Remove(line);
        Recalculate();
    }

    public void Submit(DateTimeOffset now)
    {
        EnsureDraft("submit");

        if (_lines.Count == 0)
            throw new InvalidOperationException($"Claim {Number} has no lines and cannot be submitted.");

        Status = ClaimStatus.Submitted;
        SubmittedOn = now;

        Raise(new ClaimSubmitted(Id, Number, PayerCode, Total.Amount, Total.Currency));
    }

    public void Accept()
    {
        EnsureStatus(ClaimStatus.Submitted, "accept");
        Status = ClaimStatus.Accepted;
    }

    public void Reject(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureStatus(ClaimStatus.Submitted, "reject");

        Status = ClaimStatus.Rejected;
        RejectionReason = reason;

        Raise(new ClaimRejected(Id, Number, reason));
    }

    public void MarkPaid(DateTimeOffset now)
    {
        if (Status is not (ClaimStatus.Submitted or ClaimStatus.Accepted))
            throw new InvalidOperationException($"Claim {Number} is {Status} and cannot be marked paid.");

        Status = ClaimStatus.Paid;
        PaidOn = now;
        IsFlaggedForChase = false;

        Raise(new ClaimPaid(Id, Number, Total.Amount, Total.Currency));
    }

    public void Void(string reason)
    {
        if (Status == ClaimStatus.Paid)
            throw new InvalidOperationException($"Claim {Number} has been paid and cannot be voided.");

        Status = ClaimStatus.Voided;
        RejectionReason = reason;
    }

    /// <summary>Set by the chaser worker so a report can list what has already been chased.</summary>
    public void FlagForChase() => IsFlaggedForChase = true;

    /// <summary>
    /// Re-prices every line through the supplied rule. The rule is a Strategy
    /// (handbook §1.6) — the claim knows there is one, never which one.
    /// </summary>
    public void Reprice(IPricingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        EnsureDraft("reprice");

        foreach (var line in _lines)
            line.OverridePrice(rule.PriceFor(line.ProcedureCode, line.Quantity));

        Recalculate();
    }

    private void Recalculate() =>
        Total = _lines.Count == 0
            ? Money.Zero()
            : _lines.Aggregate(Money.Zero(_lines[0].UnitPrice.Currency), (sum, line) => sum + line.LineTotal);

    private void EnsureDraft(string action)
    {
        if (Status != ClaimStatus.Draft)
            throw new InvalidOperationException($"Cannot {action} claim {Number}: it is {Status}, not Draft.");
    }

    private void EnsureStatus(ClaimStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Cannot {action} claim {Number}: it is {Status}, not {expected}.");
    }
}
