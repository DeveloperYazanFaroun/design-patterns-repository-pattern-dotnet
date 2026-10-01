# Exercises — Design Patterns &amp; the Repository Pattern

**Trainee workbook.** Eighteen exercises, one codebase, four learning objectives.

Work through them in order; each part builds on the one before. Everything you need is in
this repository plus `docs/Design-Patterns-and-the-Repository-Pattern.pdf`.

> Prepared by **Yazan Faroun**.

---

## How to use this workbook

**Before you start**

```bash
dotnet build          # must succeed
dotnet test           # must be green — that is your safety net
git checkout -b lab/<your-name>
```

**The rules**

1. **`dotnet test` must be green before you move to the next exercise.** If you break
   something, the failing test is telling you what the pattern was protecting.
2. **Write the test first** where the exercise says so. It is faster, not slower.
3. **No `IQueryable<T>` leaves a repository.** Ever. If an exercise seems to need one,
   re-read §2.8 — the answer is a specification.
4. **No repository calls `SaveChanges`.** That is the Unit of Work's job, always.
5. Commit after each exercise with the exercise number in the message:
   `git commit -m "Exercise 4: ProcedureCodeSpec"`.

**Difficulty**

| Mark | Meaning | Rough time |
|---|---|---|
| 🟢 | Reading and small edits | 10–20 min |
| 🟡 | Write real code, follow an existing shape | 30–45 min |
| 🔴 | Design decisions, no shape to copy | 60–90 min |

**Marking.** Each exercise lists *acceptance criteria*. You are done when every box is
ticked and the suite is green — not when the code compiles.

---

# Part 1 — Recognise the three categories

> **Objective 1.** Recognise the three design pattern categories: creational, structural,
> behavioural.

## Exercise 1 — Classify without looking 🟢

Fill in the table from memory first, then check yourself with `GET /api/patterns`.

| # | Situation | Category | Pattern |
|---|---|---|---|
| 1 | A report exporter must support CSV, XLSX and PDF, chosen from a dropdown at run time. | | |
| 2 | A legacy payments SDK exposes `DoPay(string xml)`; your code expects `IPaymentGateway`. | | |
| 3 | Every outbound HTTP call must be retried and logged, without editing any client. | | |
| 4 | Building a `Prescription` needs eleven optional fields and three validation rules. | | |
| 5 | When a claim is approved, four unrelated modules must react — and more are coming. | | |
| 6 | Import jobs all follow parse → validate → persist → notify, but each parses differently. | | |
| 7 | Your service needs a different `ITenantConnection` depending on a request header. | | |
| 8 | A checkout screen calls seven subsystems; the caller should make one call. | | |
| 9 | You need to show a folder tree where a folder and a file behave the same way. | | |
| 10 | A document should not be fetched from blob storage until someone actually opens it. | | |

**Acceptance criteria**

- [ ] All ten rows filled in.
- [ ] For each one, you can say in one sentence *what would get harder* without the pattern.

---

## Exercise 2 — Run and read 🟢

```bash
dotnet run --project src/PatternsLab.Api
curl -X POST http://localhost:5080/api/patterns/decorator/run
curl -X POST http://localhost:5080/api/patterns/strategy/run
curl -X POST http://localhost:5080/api/patterns/observer/run
```

Then open the three matching files under `src/PatternsLab.Patterns/`.

**Answer in writing (3–4 sentences each):**

1. In `DecoratorDemo`, the database is hit once for three reads. Which class made that
   true, and what would you change to make retry wrap caching instead of the reverse?
2. In `StrategyDemo`, what exactly would you edit to add a `capitation` plan? List every
   file.
3. In `ObserverDemo`, `CLM-003` is never seen by the fraud module. Why — and what line
   would you delete to change that?

**Acceptance criteria**

- [ ] Three written answers.
- [ ] Answer 2 lists **two** files: a new strategy class and the registration.

---

## Exercise 3 — Add the 24th pattern 🟡

The catalogue claims to be open for extension. Prove it.

Add a **Null Object** demo (not a GoF pattern, which is the point — the catalogue should
not care).

**Steps**

1. Create `src/PatternsLab.Patterns/Behavioural/NullObjectDemo.cs`.
2. Show a `INotificationSink` with a real implementation and a `NullNotificationSink` that
   does nothing, and a client that needs no `if (sink is not null)` anywhere.
3. Register nothing. Run the app and hit `GET /api/patterns`.

**Acceptance criteria**

- [ ] The new pattern appears in `GET /api/patterns` with **no change to any existing file**.
- [ ] `POST /api/patterns/null-object/run` returns a transcript.
- [ ] You can explain why no registration was needed — name the mechanism.
- [ ] `PatternCatalogueTests.All_23_gang_of_four_patterns_are_present` now fails. Fix the
      test *correctly* (do not simply change 23 to 24 — the test's intent is to guard the
      GoF set; make that intent explicit in code).

---

# Part 2 — The Repository Pattern's intent and structure

> **Objective 2.** Explain the Repository Pattern's intent and structure.

## Exercise 4 — Write a specification 🟡

Add a rule: **"claims for a given procedure code, submitted in a date range, newest
first."**

**Steps**

1. Write the test first, in `tests/PatternsLab.UnitTests/Domain/SpecificationTests.cs`.
   Compile `spec.Criteria` and assert against in-memory `Claim` objects — no database.
2. Add `ClaimsByProcedureSpec` to
   `src/PatternsLab.Domain/Claims/Specifications/ClaimSpecifications.cs`.
3. Add an integration test in `RepositoryTests` proving it **translates** to SQL and
   returns the right rows.
4. Expose it at `GET /api/claims/by-procedure/{code}`.

**Hints**

- Filtering on a value inside the owned `Lines` collection needs `Any()`:
  `c => c.Lines.Any(l => l.ProcedureCode == code)`.
- Two collection includes on one query is where `SplitQuery()` starts earning its keep.

**Acceptance criteria**

- [ ] A unit test that runs with no database.
- [ ] An integration test that runs against SQLite.
- [ ] The specification is read-only and paged.
- [ ] No `IQueryable` anywhere in the endpoint or the repository interface.
- [ ] The endpoint returns a `PagedResult` shape, not a bare list.

---

## Exercise 5 — Find the leak 🟢

Someone has proposed this pull request:

```csharp
public interface IClaimRepository : IRepository<Claim>
{
    IQueryable<Claim> Query();
    Task<List<Claim>> FindAsync(Expression<Func<Claim, bool>> predicate);
    Task<Claim> GetAllAndFilterInMemoryAsync(string payer);
}
```

**Answer in writing:**

1. Name the specific problem with **each** of the three methods.
2. For each, write the replacement you would ask for instead.
3. One of the three is worse than the other two. Which, and why?

**Acceptance criteria**

- [ ] Three problems named, using the handbook's vocabulary (§2.8, §4.3).
- [ ] Three concrete replacements.
- [ ] You identified `GetAllAndFilterInMemoryAsync` as the worst and can say why in one
      sentence about the database, not about style.

---

## Exercise 6 — Prove the index matters 🟡

`ClaimConfiguration` declares
`IX_Claims_Payer_Status_SubmittedOn`.

**Steps**

1. Switch the API to SQL Server (see the README) and point it at a local database.
2. Seed a few thousand claims — write a throwaway endpoint or a test that does it.
3. Capture the SQL for `OutstandingClaimsSpec`. Set
   `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` and read the log.
4. Run it, then drop the index, then run it again. Compare the plans.

**Answer in writing:**

1. Paste the generated SQL. Which columns does the `WHERE` touch, and in what order?
2. Does the index column order match? If not, what order would be better and why?
3. `AsNoTracking()` is on this specification. What did that change — and what did it *not*
   change about the SQL?

**Acceptance criteria**

- [ ] The generated SQL is pasted into your answer.
- [ ] You can state whether the index is used and why.
- [ ] You correctly note that `AsNoTracking` changes nothing about the SQL, only what EF
      Core does with the rows afterwards.

---

## Exercise 7 — Explain it to someone else 🟢

Write a 250-word explanation of the Repository Pattern for a developer who has never seen
it, using **this codebase** as the example.

It must answer, in this order:

1. What problem does it solve? (Point at a specific file that would otherwise be worse.)
2. Who are the participants, and which project does each live in?
3. What is the one rule that, if broken, makes the whole thing pointless?

**Acceptance criteria**

- [ ] Under 300 words.
- [ ] Names at least three real files from this repository.
- [ ] Does **not** use "so we can swap the database" as the justification.

---

# Part 3 — Implement a generic repository with Unit of Work

> **Objective 3.** Implement a generic repository with a Unit of Work pattern.

## Exercise 8 — Add an aggregate, end to end 🔴

Add a **`Payer`** aggregate: `Code`, `Name`, `IsActive`, `ContactEmail`.

**Steps**

1. `Domain/Payers/Payer.cs` — aggregate root, private setters, a `Register` factory, and a
   `Deactivate(string reason)` that raises `PayerDeactivated`.
2. `Domain/Payers/IPayerRepository.cs` — inherit `IRepository<Payer>`, add
   `GetByCodeAsync` and `CodeExistsAsync`.
3. `Domain/Payers/Specifications/` — `ActivePayersSpec`.
4. `Infrastructure/Persistence/Configurations/PayerConfiguration.cs` — unique index on
   `Code`, max lengths, audit columns.
5. `Infrastructure/Repositories/EfPayerRepository.cs`.
6. Add `Payers` to `IUnitOfWork` and `UnitOfWork`.
7. Register it in the composition root.
8. `GET /api/payers` and `POST /api/payers/{code}/deactivate`.
9. Tests: unit tests for the aggregate rules, an integration test for the specification.

**Acceptance criteria**

- [ ] `PatternsLab.Domain` still has zero EF Core references.
- [ ] `Payer` has no public setters.
- [ ] `IPayerRepository` exposes no `IQueryable` and no `SaveChanges`.
- [ ] Creating a duplicate code returns **409** with a message naming the field.
- [ ] `PayerDeactivated` is dispatched, and you can see it in `GET /api/audit`.
- [ ] Deleting a payer soft-deletes it, with no extra code in your repository.
- [ ] `dotnet test` is green.

---

## Exercise 9 — Make the transaction actually matter 🟡

`ClaimSubmissionService.SubmitAsync` changes two aggregates in one transaction. Prove the
rollback works, then break it and watch it fail.

**Steps**

1. Write an integration test that: submits a claim, forces an exception **after**
   `SaveChangesAsync` inside the transaction, and asserts that **neither** the claim nor
   the patient's counter was written. (`UnitOfWorkTests` has one to copy from.)
2. Now comment out `uow.ExecuteInTransactionAsync(...)` and call the body directly.
3. Re-run your test. Explain the failure in one sentence.
4. Put it back.

**Acceptance criteria**

- [ ] A passing rollback test.
- [ ] A written sentence explaining exactly what was left behind without the transaction.
- [ ] You can state why `CreateExecutionStrategy()` must wrap the transaction and not the
      other way round.

---

## Exercise 10 — Add a third decorator 🟡

Add a `MetricsClaimRepository` that counts calls per method and exposes the counts at
`GET /api/diagnostics/repository-metrics`.

**Steps**

1. Implement `IClaimRepository`, delegating everything.
2. Register it in the composition root so the chain becomes
   **Metrics → Logging → Caching → Ef**.
3. Hit some endpoints, then read the metrics.

**Then change the order** so it becomes Logging → Metrics → Caching → Ef.

**Answer in writing:**

1. Which ordering counts cache hits, and which does not?
2. Which ordering would you ship, and why?

**Acceptance criteria**

- [ ] Three decorators in the chain, working.
- [ ] Metrics visible over HTTP.
- [ ] A written answer showing you understand that order is a semantic decision, not a
      stylistic one.

---

## Exercise 11 — Break a lifetime on purpose 🟡

**Steps**

1. In `OutstandingClaimChaser`, replace `IServiceScopeFactory` with `IUnitOfWork` in the
   constructor. It compiles.
2. Run the app. Capture the exact exception.
3. Revert.

**Answer in writing:**

1. Paste the exception. Which container rule produced it?
2. If the container had *not* caught it, name two things that would have gone wrong later
   at run time.
3. Which registrations in this solution are singletons, and why is each one safe?

**Acceptance criteria**

- [ ] The real exception text is pasted.
- [ ] You name "captive dependency" and can point at handbook Table 3.1.
- [ ] You correctly identify `InMemoryAuditTrail`, `IClock` and `IPricingRule` as safe
      singletons, and say what they have in common.

---

## Exercise 12 — Fix the paging bug 🟡

**Steps**

1. In `SpecificationEvaluator.Apply`, delete the `if (forCount) return query;` line.
2. Run `dotnet test`. One test should fail.
3. Read the failure, then restore the line.

**Answer in writing:**

1. Which test caught it, and what did the user-visible symptom look like?
2. Why does skipping `Include` for a count query also matter — what does it save?

**Acceptance criteria**

- [ ] You name `CountAsync_ignores_paging_so_page_totals_are_correct`.
- [ ] You can describe the symptom as a user would report it.

---

## Exercise 13 — Silent no-op 🟡

**Steps**

1. In `OutstandingClaimChaser`, change `tracked: true` to `tracked: false`.
2. Run the worker (set `Workers:ClaimChaser:IntervalMinutes` to 1).
3. Watch the log line report "0 row(s) updated" while claims were clearly flagged.

**Answer in writing:**

1. Why did nothing save, even though `FlagForChase()` definitely ran?
2. Write the test you would add so this can never regress.
3. Actually add it.

**Acceptance criteria**

- [ ] A failing-then-passing test that would catch an untracked mutation.
- [ ] You can explain change tracking in two sentences without using the word "magic".

---

## Exercise 14 — Concurrency, for real 🔴

**Steps**

1. Open `/api/claims/{id}` in two browser tabs.
2. `POST /api/claims/{id}/pay` in the first.
3. `POST /api/claims/{id}/reprice` in the second.
4. Observe.
5. Now make the API honest: add an `If-Match` header carrying the row version, return
   **412 Precondition Failed** when it does not match, and include the current version in
   every claim response as an `ETag`.

**Acceptance criteria**

- [ ] A second writer gets 412, not a silent overwrite.
- [ ] The `ETag` round-trips.
- [ ] An integration test covers the conflict path.
- [ ] `ConcurrencyConflictException` is still the only concurrency type the application
      layer sees — no `DbUpdateConcurrencyException` above Infrastructure.

---

# Part 4 — Judgement: value vs. unnecessary layers

> **Objective 4.** Identify when the pattern adds value and when it adds unnecessary layers.

## Exercise 15 — Review a real pull request 🟡

Here is the PR. Review it as you would a colleague's.

```csharp
// ── Domain/Abstractions/IGenericRepository.cs
public interface IGenericRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> GetByIdAsync(int id);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task SaveAsync();
}

// ── Infrastructure/GenericRepository.cs
public class GenericRepository<T>(AppDbContext db) : IGenericRepository<T> where T : class
{
    public async Task<IEnumerable<T>> GetAllAsync() => await db.Set<T>().ToListAsync();
    public async Task<T> GetByIdAsync(int id) => await db.Set<T>().FindAsync(id);
    public async Task AddAsync(T entity) { db.Set<T>().Add(entity); await db.SaveChangesAsync(); }
    public async Task UpdateAsync(T entity) { db.Set<T>().Update(entity); await db.SaveChangesAsync(); }
    public async Task DeleteAsync(int id) { db.Set<T>().Remove(await GetByIdAsync(id)); await db.SaveChangesAsync(); }
    public async Task SaveAsync() => await db.SaveChangesAsync();
}

// ── Api/ClaimsController.cs
public class ClaimsController(IGenericRepository<Claim> repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string payer)
    {
        var all = await repo.GetAllAsync();
        return Ok(all.Where(c => c.PayerCode == payer && c.Status != ClaimStatus.Paid));
    }
}
```

**Write a review with at least six findings.** For each: what is wrong, why it matters, and
the concrete change you want.

**Acceptance criteria**

- [ ] At least six distinct findings.
- [ ] You flagged the in-memory filtering in the controller as the **most severe**, and
      explained it in terms of rows crossing the wire.
- [ ] You flagged `SaveAsync` *and* the per-method `SaveChangesAsync` as two separate
      problems.
- [ ] You noted `int id` versus this solution's `Guid`, and why that matters for a generic
      contract.
- [ ] Your review is specific enough that the author could act on it without asking a
      follow-up question.

---

## Exercise 16 — Argue the other side 🟡

In pairs. One of you argues **for** introducing repositories in each scenario, the other
**against**. Five minutes each, then swap sides.

| # | Scenario |
|---|---|
| 1 | Internal admin tool: 12 tables, 3 developers, pure CRUD, no domain rules. |
| 2 | Billing engine: 40 aggregates, audit obligations, 14 developers across 3 teams. |
| 3 | Reporting API: 60 read-only endpoints, heavy aggregation, no writes at all. |
| 4 | Green-field product; nobody on the team has used the pattern before. |
| 5 | Existing codebase with an `IRepository<T>` that only exposes CRUD. |
| 6 | A mobile BFF that mostly forwards calls to three upstream services. |

**Acceptance criteria**

- [ ] For each scenario, one sentence of recommendation plus the single strongest
      counter-argument.
- [ ] At least two of your six recommendations are **"do not add it"**. If all six are
      "add it", you have not understood Part 4.

---

## Exercise 17 — Delete a layer 🔴

`ClaimQueryService` has one implementation and will only ever have one.

**Steps**

1. Make the case, in writing, for deleting it and letting the endpoints call
   `IUnitOfWork` directly.
2. Make the case for keeping it.
3. Decide. Then **actually do it** on a branch — either delete it or write down the
   specific future change that justifies it.

**Acceptance criteria**

- [ ] Both cases written honestly — the one you disagree with must still be its strongest form.
- [ ] A decision, with a reason that refers to something observable in *this* codebase
      rather than a general principle.
- [ ] `dotnet test` green after whichever branch you took.

---

## Exercise 18 — The checklist, applied 🟡

Run the handbook's §4.6 checklist over this entire repository.

| Green flag | Pass? | Evidence (file + line) |
|---|---|---|
| Methods and specs named after business concepts | | |
| Exactly one repository per aggregate root | | |
| No `IQueryable` / `Expression` / EF Core type in an interface | | |
| `SaveChangesAsync` on the Unit of Work only | | |
| Service tests run with no database | | |
| Query correctness covered by integration tests | | |
| Cross-cutting write concerns appear once | | |

| Red flag | Present? | Evidence |
|---|---|---|
| Methods are only `GetAll` / `GetById` / `Add` / `Update` / `Delete` | | |
| Interface with one implementation and no added vocabulary | | |
| Callers still write `Include` and `Where` after calling it | | |
| Repositories call `SaveChanges` | | |
| A repository for a child entity | | |
| Justification is "we might change database" | | |
| One screen means editing four logic-free files | | |

**Then find at least one place where this repository itself falls short**, and either fix
it or write down why you would leave it.

**Acceptance criteria**

- [ ] Both tables filled with file references, not opinions.
- [ ] At least one honest criticism of this codebase.
- [ ] A one-paragraph verdict: is the layer earning its place here? Would it in *your*
      project?

---

## Finishing up

```bash
dotnet test
git add .
git commit -m "Lab complete"
git push -u origin lab/<your-name>
```

Bring to the review session:

1. Your written answers to exercises 2, 5, 7, 10, 11, 12, 13, 15, 16, 17 and 18.
2. Your branch.
3. **One thing you disagree with** in the handbook. There is at least one — Part 4 exists
   because reasonable people argue about this.
