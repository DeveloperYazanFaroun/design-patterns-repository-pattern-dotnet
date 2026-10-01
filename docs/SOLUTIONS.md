# Solutions &amp; marking notes

**Trainer copy.** Worked answers, the mistakes to expect, and what a good answer sounds
like versus a passing one.

> Prepared by **Yazan Faroun**.

---

## Exercise 1 — Classify without looking

| # | Category | Pattern | Without it |
|---|---|---|---|
| 1 | Behavioural | **Strategy** | A `switch` on format that grows every release |
| 2 | Structural | **Adapter** | Vendor XML and vendor exceptions leak into your services |
| 3 | Structural | **Decorator** | Retry/logging copy-pasted into every HTTP client |
| 4 | Creational | **Builder** | An eleven-argument constructor and half-built objects |
| 5 | Behavioural | **Observer** | The approval method edited for every new consumer |
| 6 | Behavioural | **Template Method** | Four import jobs that each got the order slightly wrong |
| 7 | Creational | **Factory Method** / Abstract Factory | `new` on a concrete connection inside business code |
| 8 | Structural | **Facade** | Seven-call sequences duplicated and drifting |
| 9 | Structural | **Composite** | `if (node is Folder)` in every traversal |
| 10 | Structural | **Proxy** | Blob storage hit for documents nobody opens |

**Common wrong answers**

- #3 answered as *Proxy*. Accept it only if they can articulate the difference: a decorator
  **adds behaviour**, a proxy **controls access**. Same shape, different intent.
- #7 answered as *Singleton*. Wrong category — push back: what varies is *which type gets
  instantiated*, not *how many exist*.
- #1 answered as *Factory Method*. A factory would pick the exporter; Strategy is what makes
  the exporters interchangeable. Both can be present; Strategy is the one being asked for.

---

## Exercise 2 — Run and read

1. **`CachingClaimReader`.** To reverse the order, construct
   `new RetryingClaimReader(new CachingClaimReader(real))` instead. That version retries
   cache hits, which is pointless work, and is why the shipped order puts caching outermost.
2. **Two files:** a new `Capitation : IPricingStrategy` class, and one
   `AddSingleton<IPricingRule>` line in the composition root. `PricingContext` and
   `PricingService` are untouched — that is the whole claim of the pattern.
3. Because `fraudSubscription.Dispose()` ran first. Delete that line and fraud sees
   `CLM-003`. Note that the *subject* has no idea any of this happened.

**Marking.** Answer 2 is the one that matters. If they list three or more files, they have
not spotted that `IEnumerable<IPricingRule>` injection is doing the work.

---

## Exercise 3 — Add the 24th pattern

The mechanism they must name is **assembly scanning by reflection** in
`PatternsServiceCollectionExtensions.AddPatternCatalogue` — any public, non-abstract
`IPatternDemo` with a parameterless constructor is registered automatically.

Sketch of an acceptable answer:

```csharp
public interface INotificationSink { void Send(string message); }

public sealed class EmailSink : INotificationSink
{
    public List<string> Sent { get; } = [];
    public void Send(string message) => Sent.Add(message);
}

/// The Null Object: the "do nothing" case becomes a TYPE, not a null check.
public sealed class NullNotificationSink : INotificationSink
{
    public static readonly NullNotificationSink Instance = new();
    public void Send(string message) { }
}

public sealed class ClaimAlerter(INotificationSink sink)
{
    // No `if (sink is not null)` anywhere — that is the entire point.
    public void Alert(string claimNumber) => sink.Send($"Claim {claimNumber} needs attention.");
}

public sealed class NullObjectDemo : PatternDemo
{
    public override string Key => "null-object";
    public override string Name => "Null Object";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Give 'do nothing' a type, so callers need no null checks.";
    public override string HandbookSection => "not in the GoF 23";
    public override string SeenInDotNet => "NullLogger<T>.Instance, Stream.Null, EmptyServiceProvider";

    protected override void Execute(Action<string> log)
    {
        var real = new EmailSink();
        new ClaimAlerter(real).Alert("CLM-001");
        new ClaimAlerter(NullNotificationSink.Instance).Alert("CLM-002");

        log($"real sink received {real.Sent.Count} message(s)");
        log("null sink received it too — and did nothing, without a single null check");
    }
}
```

**The test fix.** The lazy answer is `ShouldBe(24)`. The correct answer makes the intent
explicit — the test is guarding the *GoF set*, not the total:

```csharp
[Fact]
public void All_23_gang_of_four_patterns_are_present()
{
    var catalogue = Build();

    foreach (var key in GangOfFourKeys)
        catalogue.Find(key).ShouldNotBeNull($"'{key}' is missing.");

    catalogue.Count.ShouldBeGreaterThanOrEqualTo(GangOfFourKeys.Length);
}
```

**Marking.** Accept `ShouldBe(24)` only with a written justification. Prefer the version
above and say why: a test that has to be edited every time you add a file is not protecting
anything.

---

## Exercise 4 — Write a specification

```csharp
public sealed class ClaimsByProcedureSpec : Specification<Claim>
{
    public ClaimsByProcedureSpec(
        string procedureCode, DateOnly? from = null, DateOnly? to = null,
        int page = 1, int pageSize = 25)
    {
        var code = procedureCode.Trim().ToUpperInvariant();

        Where(c => c.Lines.Any(l => l.ProcedureCode == code));

        if (from is { } f) Where(c => c.ServiceDate >= f);
        if (to is { } t) Where(c => c.ServiceDate <= t);

        AddInclude(c => c.Lines);
        AddInclude(c => c.Patient!);
        SplitQuery();
        ApplyOrderByDescending(c => c.ServiceDate);
        ApplyPaging(page, pageSize);
        ReadOnly();
    }
}
```

**What to look for**

- `Where` called repeatedly rather than one giant lambda — that is what `AndAlso` is for,
  and it is how optional filters stay readable.
- `ToUpperInvariant()` on the input, matching how `Claim.AddLine` normalises it. A trainee
  who misses this writes a spec that silently returns nothing for lowercase input — worth
  letting them discover.
- The unit test compiles `Criteria` and needs no database.
- The integration test is the one that proves `Lines.Any(...)` **translates**. A mocked
  repository would have proved nothing (§4.2) — say this out loud.

**Common mistake.** Returning `IReadOnlyList<Claim>` from the endpoint instead of a paged
result. The specification pages, so the response must report the total or the client cannot
build a pager.

---

## Exercise 5 — Find the leak

| Method | Problem | Replacement |
|---|---|---|
| `IQueryable<Claim> Query()` | The abstraction is fake. Callers bind to LINQ-to-Entities translation rules, can trigger lazy loading after disposal, and can write an unindexed cross-join from a controller. §2.8. | A specification per rule, or `SearchAsync(criteria)` |
| `FindAsync(Expression<...>)` | Marginally better, but the domain project now references `System.Linq.Expressions` *as a public contract* and callers still author queries. The repository has a method but no vocabulary. | Named specifications |
| `GetAllAndFilterInMemoryAsync` | **The worst.** Every row for every payer crosses the wire and is filtered in process memory. It is O(table) and it degrades silently as data grows — nothing fails, it just gets slower until it falls over. | A specification with the predicate in `Criteria`, evaluated in the database |

**Marking.** The required insight for full marks on #3 is *rows crossing the wire*, not
"it's slow". A trainee who says "bad practice" has not understood it.

---

## Exercise 6 — Prove the index matters

Expected SQL shape (SQL Server):

```sql
SELECT [c].[Id], [c].[Number], ... , [l].[Id], [l].[ProcedureCode], ...
FROM [Claims] AS [c]
LEFT JOIN [ClaimLines] AS [l] ON [c].[Id] = [l].[ClaimId]
LEFT JOIN [Patients] AS [p] ON [c].[PatientId] = [p].[Id]
WHERE [c].[IsDeleted] = CAST(0 AS bit)
  AND [c].[PayerCode] = @__payerCode_0
  AND [c].[Status] <> 4 AND [c].[Status] <> 5 AND [c].[Status] <> 0
  AND [c].[SubmittedOn] IS NOT NULL AND [c].[SubmittedOn] < @__cutoff_1
ORDER BY [c].[SubmittedOn] DESC
OFFSET @__p_2 ROWS FETCH NEXT @__p_3 ROWS ONLY
```

1. `IsDeleted` (from the global filter), `PayerCode`, `Status`, `SubmittedOn`.
2. The index is `(PayerCode, Status, SubmittedOn)`. `PayerCode` is an equality predicate and
   correctly leads. `Status` is a set of inequalities, which is weaker as a seek predicate —
   a strong answer notices this and proposes `(PayerCode, SubmittedOn) INCLUDE (Status)`,
   or a filtered index `WHERE IsDeleted = 0`.
3. **`AsNoTracking` changes nothing about the generated SQL.** Identical text, identical
   plan. What changes is that EF Core does not snapshot the results into the change tracker,
   which saves memory and CPU on the client. A trainee who claims the SQL changes has a
   mental model worth correcting on the spot.

---

## Exercise 7 — Explain it to someone else

A full-marks answer contains:

1. **Problem** — points at something concrete, e.g. "without it, the predicate in
   `OutstandingClaimsSpec` would be copy-pasted into `ClaimEndpoints`, the chaser worker and
   the totals report, and a change to the 30-day rule would fix two of the three."
2. **Participants** — `IClaimRepository` (Domain), `EfClaimRepository` (Infrastructure),
   `Claim` (Domain), `AppDbContext` (Infrastructure), `IUnitOfWork` spanning both.
3. **The one rule** — *do not return `IQueryable<T>`*, or equivalently *the interface must
   contain no EF Core type*. Accept either; they are the same rule seen from two sides.

**Automatic fail:** "so we can swap the database later". The exercise forbids it, and §4.2
explains why it is the weakest argument available.

---

## Exercise 8 — Add an aggregate, end to end

The largest exercise. Things that usually go wrong, in the order they usually go wrong:

1. **Public setters on `Payer`.** Then a service sets `IsActive = false` directly and the
   `PayerDeactivated` event never fires. Ask them to make every setter private and see what
   breaks.
2. **Forgetting `builder.Ignore(p => p.DomainEvents)`.** EF Core tries to map the collection
   and the model fails to build. The error message is good; let them read it.
3. **A `PayerRepository` that calls `SaveChanges`.** The most common regression of all.
4. **Registering the repository but not adding it to `UnitOfWork`.** Then
   `uow.Payers` does not exist and they inject `IPayerRepository` directly into a service —
   which works, but quietly gives up the single-commit guarantee if they also touch claims.
   Worth a discussion rather than a correction.
5. **Soft delete not working.** Because `Payer` did not implement `ISoftDeletable`. The fix
   is one interface, no repository code — which is exactly the point of doing cross-cutting
   concerns in the Unit of Work.

Skeleton:

```csharp
public sealed class Payer : AggregateRoot, IAuditable, ISoftDeletable, IHasConcurrencyToken
{
    private Payer() { }

    private Payer(string code, string name, string contactEmail)
        => (Code, Name, ContactEmail, IsActive) = (code, name, contactEmail, true);

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public string? DeactivationReason { get; private set; }

    /* IAuditable / ISoftDeletable / IHasConcurrencyToken members */

    public static Payer Register(string code, string name, string contactEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!contactEmail.Contains('@')) throw new ArgumentException("A contact email is required.");

        return new Payer(code.Trim().ToUpperInvariant(), name.Trim(), contactEmail.Trim());
    }

    public void Deactivate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!IsActive) throw new InvalidOperationException($"Payer {Code} is already inactive.");

        IsActive = false;
        DeactivationReason = reason;
        Raise(new PayerDeactivated(Id, Code, reason));
    }
}
```

---

## Exercise 9 — Make the transaction actually matter

Without `ExecuteInTransactionAsync`, the inner `SaveChangesAsync` commits on its own. The
claim is written and the exception then aborts the request — leaving a submitted claim
whose patient counter was never incremented. **Data that disagrees with itself**, which is
the failure mode the pattern is named after.

`CreateExecutionStrategy()` must wrap the transaction because a retry has to re-run the
*whole* unit. If the strategy sat inside, a retried statement would execute against a
transaction that had already been rolled back. EF Core throws a specific, helpful exception
if you get this backwards with `EnableRetryOnFailure` on — worth showing them.

---

## Exercise 10 — Add a third decorator

1. **Metrics outside caching counts every call, including cache hits. Metrics inside
   caching counts only the calls that reached the database.**
2. Either is defensible and the answer must say *what question you are answering*:
   - outermost → "how often does the application ask for this?"
   - inner → "how much load do we put on the database?"

A trainee who says "outermost, obviously" without naming the question has not understood
that this is a semantic choice. Push once.

---

## Exercise 11 — Break a lifetime on purpose

Expected exception (text varies slightly by version):

```
System.AggregateException: Some services are not able to be constructed
 ---> InvalidOperationException: Cannot consume scoped service
      'PatternsLab.Domain.Abstractions.IUnitOfWork' from singleton
      'Microsoft.Extensions.Hosting.IHostedService'.
```

The rule is **scope validation**, on by default in Development.

Had it not been caught: (a) the `DbContext` would be shared across overlapping runs and it
is not thread-safe; (b) its change tracker would accumulate every entity ever loaded, so
memory grows without bound and `SaveChanges` gets slower every cycle. Accept also: stale
reads, because a long-lived context serves cached instances.

Safe singletons here: `InMemoryAuditTrail`, `SystemClock`, the three `IPricingRule`
implementations, `PatternCatalogue`, the pattern demos, `LegacySmtpGateway`. **What they
have in common: none of them holds a scoped dependency, and all are thread-safe.** That is
the whole test.

---

## Exercise 12 — Fix the paging bug

`CountAsync_ignores_paging_so_page_totals_are_correct` fails: the count returns 3 instead of
7, because `Skip`/`Take` were applied to the counting query.

User-visible symptom: *"every page says 'showing 1–3 of 3', and the pager never lets me go
past page 1."*

Skipping `Include` for counts removes the joins, so the database reads fewer pages and does
no work assembling rows it will immediately discard. On a wide aggregate this is
measurable, and it is free.

---

## Exercise 13 — Silent no-op

1. `AsNoTracking()` means EF Core does not keep a snapshot of the entity, so at
   `SaveChanges` time there is nothing to compare against and no `UPDATE` is generated.
   The mutation happened — to an object the context has no opinion about.
2 & 3. The test:

```csharp
[Fact]
public async Task Flagging_an_untracked_claim_saves_nothing()
{
    var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
    var claim = fx.AddSubmittedClaim(patient.Id, $"NT-{Guid.NewGuid():N}"[..12], "PBM-NT", 45);

    var untracked = await fx.Uow.Claims.ListAsync(
        new OutstandingClaimsSpec("PBM-NT", fx.Clock.UtcNow));           // read-only

    untracked.Single().FlagForChase();
    (await fx.Uow.SaveChangesAsync()).ShouldBe(0);                        // proves the trap

    var tracked = await fx.Uow.Claims.ListAsync(
        new OutstandingClaimsSpec("PBM-NT", fx.Clock.UtcNow, tracked: true));

    tracked.Single().FlagForChase();
    (await fx.Uow.SaveChangesAsync()).ShouldBeGreaterThan(0);             // proves the fix
}
```

**Marking.** A test that only asserts the *fixed* path is worth half marks. The valuable
test is the one that pins the trap, because that is the regression you are guarding.

---

## Exercise 14 — Concurrency, for real

Without `If-Match`, step 4 shows **last writer wins** — the reprice silently overwrites the
payment, or vice versa, depending on ordering.

Acceptable implementation:

```csharp
group.MapPost("/{id:guid}/pay", async (
    ClaimPaymentService payments, Guid id, HttpContext http, CancellationToken ct) =>
{
    byte[]? expected = http.Request.Headers.IfMatch is [var tag, ..] && tag is not null
        ? Convert.FromBase64String(tag.Trim('"'))
        : null;

    var result = await payments.MarkPaidAsync(new MarkClaimPaidCommand(id, expected), ct);

    return result.Status switch
    {
        ResultStatus.Success  => Results.NoContent(),
        ResultStatus.Conflict => Results.StatusCode(StatusCodes.Status412PreconditionFailed),
        ResultStatus.NotFound => Results.NotFound(),
        _ => Results.BadRequest(new { error = result.Error })
    };
});
```

and on the read side, `http.Response.Headers.ETag = $"\"{Convert.ToBase64String(claim.RowVersion!)}\"";`.

**The point to land:** the API layer converts a *domain* conflict into an HTTP status. It
never sees `DbUpdateConcurrencyException`, because `UnitOfWork` already translated it. If a
trainee catches the EF Core exception in an endpoint, that is the finding.

---

## Exercise 15 — Review a real pull request

Findings, most severe first:

1. **In-memory filtering in the controller.** `GetAllAsync()` then `.Where(...)` in LINQ to
   Objects pulls the entire `Claims` table across the wire on every request. It works
   perfectly in dev with 20 rows and takes the site down at 2 million.
2. **`SaveChangesAsync` inside `AddAsync`/`UpdateAsync`/`DeleteAsync`.** Two aggregates in
   one use case now means two transactions, and no caller can restore atomicity.
3. **`SaveAsync` as well.** Two ways to commit, neither of which is a unit of work. Which
   one is authoritative?
4. **`GetAllAsync` exists at all.** There is no bounded version of this method, so every
   caller either fetches everything or writes its own filter (see finding 1).
5. **No vocabulary.** Five CRUD methods and nothing named after the business. This is the
   configuration handbook §4.5 scores as useless — it adds a layer and a mock and buys
   nothing.
6. **`where T : class`** instead of a domain base type. Any class at all can be passed,
   including DTOs and EF Core's own types. `where T : Entity` restores the constraint.
7. **`int id`.** Couples every aggregate to identity-column keys. This solution uses
   `Guid` (version 7) precisely so ids can be generated client-side before the insert.
8. **`Task<T> GetByIdAsync` is not nullable.** `FindAsync` returns null for a miss, so the
   signature lies and every caller gets a warning-free `NullReferenceException`.
9. **`FindAsync` bypasses global query filters**, so a soft-deleted row comes back from
   `GetByIdAsync` but not from a query. Inconsistent and hard to debug.

Six of these nine is a pass. Missing #1 is a fail regardless of how many others they found.

---

## Exercise 16 — Argue the other side

| # | Recommendation | Strongest counter |
|---|---|---|
| 1 | **No.** Use the DbContext directly. | With 12 tables and no rules there is no vocabulary to name and nothing to test without a database. |
| 2 | **Yes**, with specifications and a Unit of Work. | Audit obligations alone justify the cross-cutting write hook; 14 developers need a constrained surface. |
| 3 | **No** — CQRS projections instead. | Repositories return aggregates; a reporting API never wants an aggregate. The write side is where repositories live. |
| 4 | **Not yet.** Start with specific repositories when duplication appears. | Introducing generics a team has never used produces the CRUD-only version, which is the worst of both worlds. |
| 5 | **Fix or delete.** Either add vocabulary or remove the layer. | Keeping a pass-through out of politeness is how codebases calcify. |
| 6 | **No.** There is no persistence to mediate. | A BFF's "repository" would wrap HTTP clients — that is an Adapter, and it should be called one. |

At least two "no" answers required. If a trainee says yes to all six, the conversation to
have is §4.4: the decision procedure asks about *observed facts in the codebase today*, not
about architecture you hope to have.

---

## Exercise 17 — Delete a layer

**For deleting:** it has one implementation, no interface, no test doubles pointed at it,
and every method is a one-line delegation to `uow.Claims`. By the §4.6 red-flag list it is a
pass-through.

**For keeping:** it is the only place a query rule is *chosen*. Endpoints currently name a
specification; if that choice moved into the endpoints, the same specification could be
constructed three different ways in three endpoints — exactly the duplication the pattern
exists to prevent. It is also the natural seam if the read side later moves to CQRS.

**Either decision is acceptable.** What is being marked is whether the reason refers to
something *observable in this codebase* — "three endpoints construct `ClaimSearchSpec`
differently" is an argument; "separation of concerns" is not.

---

## Exercise 18 — The checklist, applied

Green flags, all pass, with evidence:

| Flag | Evidence |
|---|---|
| Business names | `OutstandingClaimsSpec`, `GetOutstandingAsync`, `TotalsByPayerAsync` |
| One per aggregate root | `IClaimRepository`, `IPatientRepository` — no `IClaimLineRepository` |
| No EF Core in interfaces | `grep -r EntityFrameworkCore src/PatternsLab.Domain` returns nothing |
| `SaveChangesAsync` in one place | `UnitOfWork.SaveChangesAsync` |
| Fast service tests | `ClaimSubmissionServiceTests` — no fixture, no database |
| Query correctness covered | `RepositoryTests` against SQLite |
| Cross-cutting once | `StampAuditFields`, `ConvertDeletesToSoftDeletes` |

**Honest criticisms of this codebase** — award credit for any of these, and for anything
else defensible:

1. `ClaimQueryService` is close to a pass-through (Exercise 17).
2. `Repository<T>.PageAsync` issues two queries; a single windowed query with
   `COUNT(*) OVER()` would halve the round trips.
3. `SeedData` reaches for `AppDbContext` directly rather than going through the repository —
   defensible for bootstrapping, but it *is* an exception to the rule the project preaches.
4. `CachingClaimRepository` caches only `NumberExistsAsync`, so as a demonstration of
   caching it is thin.
5. The `IClock` abstraction duplicates `TimeProvider`, which .NET now ships.
6. There is no outbox, so a domain-event handler that fails after commit loses its side
   effect silently.
7. `InMemoryAuditTrail` bounds itself by dropping the oldest entries — fine for a demo,
   wrong for anything called "audit".

A trainee who finds #3 or #6 unprompted has read carefully. Say so.
