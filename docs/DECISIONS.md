# Design decisions

Why this codebase is shaped the way it is — including the things it deliberately does
*not* do. Read this before arguing with the code; most of the arguments are already here.

> Prepared by **Yazan Faroun**.

---

## 1. The domain project has no EF Core reference

**Decision.** `PatternsLab.Domain` references nothing but the BCL.

**Why.** It is the only mechanical guarantee that the abstraction is real. Anyone can
*say* their repository hides persistence; a project file that cannot compile against
`Microsoft.EntityFrameworkCore` proves it. You can check the claim in one command:

```bash
grep -r "EntityFrameworkCore" src/PatternsLab.Domain   # must print nothing
```

**Cost.** `Specification<T>` has to be evaluated somewhere, so
`SpecificationEvaluator` lives in Infrastructure and the two must be kept in step. That is
a real maintenance tax, paid once.

---

## 2. Repositories return results, never `IQueryable<T>`

**Decision.** No method anywhere returns `IQueryable<T>` or takes an
`Expression<Func<T, bool>>` as a public parameter.

**Why.** Returning `IQueryable` gives callers the full power of LINQ *and* its full set of
failure modes: silent client-side evaluation, lazy loading after the context is disposed,
and expressions the provider cannot translate. You gain a file and lose the guarantee the
pattern exists to provide (handbook §2.8).

**The objection this raises** — "then how do I do forty filter combinations?" — is answered
by `ClaimSearchSpec`. Optional filters compose with `Where(...)` calls; the specification
grows, the interface does not.

---

## 3. Specifications, not a method per query

**Decision.** Query rules are objects (`Specification<T>`), not repository methods.

**Why.** The alternative is `GetByPayerAndStatusOrderedByDatePagedIncludingLinesAsync`,
which is handbook §4.3 anti-pattern 3. A specification can be named, composed, unit-tested
without a database, and reused by an endpoint, a worker and a report.

**What we did not do.** We did not take a dependency on `Ardalis.Specification`, even
though it is good and production-grade. The whole point of this repository is that a
trainee can read the 90 lines that make it work. In a real project, adopt the library.

---

## 4. `Repository<T>` is generic, but `IClaimRepository` exists too

**Decision.** A shared generic base for mechanics, plus a per-aggregate interface for
vocabulary.

**Why.** A CRUD-only `IRepository<T>` over EF Core is the configuration handbook §4.5 scores
as useless — it adds a layer and buys nothing, and it is the exhibit critics reach for.
The generic base earns its place only because `EfClaimRepository` adds
`GetOutstandingAsync`, `NumberExistsAsync` and `TotalsByPayerAsync` on top of it.

**Rule of thumb from §4.7:** do not add the generic base until there are three or more
repositories repeating the same mechanics. Below that, the duplication is cheaper than the
generics.

---

## 5. No repository calls `SaveChanges`

**Decision.** `SaveChangesAsync` exists on exactly one type, `UnitOfWork`.

**Why.** A repository that commits makes atomic multi-aggregate operations impossible, and
no caller can put it back. `ClaimSubmissionService.SubmitAsync` changes a claim *and* a
patient; with per-repository saves, a failure between them leaves data that disagrees with
itself.

This is the single easiest rule to break by accident, which is why
`UnitOfWorkTests.A_failure_inside_the_transaction_rolls_everything_back` exists.

---

## 6. Cross-cutting write concerns live in the Unit of Work

**Decision.** Audit stamping, soft delete and domain-event dispatch happen inside
`SaveChangesAsync`, for every aggregate, on every write path.

**Why.** This is the strongest honest justification for the Unit of Work in this codebase —
much stronger than "we might change database". Written once, it applies to code nobody has
written yet.

**The ordering matters and is deliberate:**

1. stamp audit fields
2. convert deletes to soft deletes (which turns a `Deleted` entry into `Modified`)
3. stamp concurrency tokens
4. collect domain events
5. `SaveChangesAsync`
6. dispatch events — **after** the commit, never before

Step 6 is why a rolled-back transaction cannot send an email saying the claim was
submitted.

---

## 7. Domain events are dispatched after commit, and failures are swallowed

**Decision.** `DomainEventDispatcher` logs and continues when a handler throws.

**Why.** The business transaction has already committed. Throwing would fail a request
whose work actually succeeded, and the caller would retry an operation that already
happened.

**The honest limitation.** This means a side effect can be lost. If a side effect must be
guaranteed, it belongs in an **outbox** — write the message in the same transaction, deliver
it separately. This project does not implement one, on purpose: it is the next topic, not
this one. (It is also a valid answer to Exercise 18.)

---

## 8. Soft delete is a global query filter, not repository code

**Decision.** `AppDbContext.OnModelCreating` applies `HasQueryFilter` to every
`ISoftDeletable`, and `UnitOfWork` turns `Deleted` into `Modified`.

**Why.** The alternative is `&& !c.IsDeleted` repeated in forty queries, one of which will
eventually be forgotten — and that one produces a screen showing records everyone else
believes are gone.

**The trap this creates, handled:** a unique index does not know about the query filter, so
a soft-deleted claim still occupies its number. `EfClaimRepository.NumberExistsAsync` calls
`IgnoreQueryFilters()` for exactly this reason, and
`RepositoryTests.NumberExistsAsync_still_sees_a_soft_deleted_claim` pins it.

---

## 9. SQLite by default, SQL Server by configuration

**Decision.** `Database:Provider` switches between them; SQLite is the default.

**Why.** `dotnet run` has to work on a clean machine, at a training session, on hotel
wifi, without installing a database.

**The one place it leaks, and it is documented in both:** the concurrency token. SQL Server
maintains `rowversion` itself; SQLite has no equivalent, so `UnitOfWork.StampConcurrencyTokens`
generates one. See `AppDbContext.ApplyConcurrencyTokens`.

This is worth pointing at during the session: it is a concrete example of an abstraction
that is *almost* provider-agnostic, and of the honest way to handle the part that is not —
name it, isolate it, test both paths.

---

## 10. `EnsureCreated`, not migrations

**Decision.** The API calls `EnsureCreatedAsync()` at startup.

**Why.** Zero friction for a first run, and the seed data makes every endpoint interesting
immediately.

**Why it is wrong for real work, stated in the README:** a database created by
`EnsureCreated` has no migrations history, so the two cannot be mixed later. Any real
project uses `dotnet ef migrations add` from day one.

---

## 11. Decorators are registered, not inherited

**Decision.** Logging and caching wrap `IClaimRepository` through a hand-written
`Decorate<TService, TDecorator>()` extension.

**Why hand-written.** Scrutor does this well and you should use it in production. Thirty
lines of `ServiceDescriptor` manipulation, written out, shows a trainee there is no magic —
and that the *order of registration* is a semantic decision, not a formatting one.

**The order shipped** is Logging → Caching → Ef, so the log records the call the application
made, including the ones the cache absorbed. Reverse it and you measure database load
instead. Both are defensible; Exercise 10 makes the trainee choose and justify.

---

## 12. Everything persistence-related is scoped

**Decision.** `AppDbContext`, `Repository<T>`, every named repository, `IUnitOfWork` and
every application service are scoped.

**Why.** Scope validation is on in Development, so a captive dependency fails at startup
rather than at 3am. `OutstandingClaimChaser` is the deliberate counter-example: it is a
singleton, so it takes `IServiceScopeFactory` and opens one scope per run.

Safe singletons in this solution — `InMemoryAuditTrail`, `SystemClock`, the `IPricingRule`
implementations, `PatternCatalogue` and the pattern demos — all share two properties: they
hold no scoped dependency, and they are thread-safe.

---

## 13. The pattern catalogue is a separate project

**Decision.** `PatternsLab.Patterns` depends on nothing but the BCL and DI abstractions.

**Why.** A trainee should be able to open `Behavioural/StrategyDemo.cs` and understand it
without loading the domain, the database or the API into their head. Each file is
self-contained on purpose, even where that means small duplications between demos.

**The trade, accepted:** `Creational/FactoryMethodDemo.cs` declares its own `INotifier`,
which is not the same type as `Application.Abstractions.INotifier`. That is deliberate.
The demo is a teaching artefact; the application type is production code; conflating them
would make both worse.

---

## 14. What is deliberately missing

| Not here | Why | Where it would go |
|---|---|---|
| **Outbox** | Guaranteed delivery is a different topic | Beside `DomainEventDispatcher` |
| **CQRS read models** | §4.5 recommends them for read-heavy systems; this app is not one | A separate `Queries` project |
| **MediatR** | Adds a dependency that hides the call graph while teaching the call graph | Application layer |
| **AutoMapper** | Same reason — the DTO mapping is three lines and should be readable | `ClaimEndpoints.ToDto` |
| **FluentValidation** | Validation lives in the aggregate here, on purpose | Application layer |
| **Authentication** | Orthogonal to the session; `ICurrentUser` is the seam | API layer |
| **Docker / CI** | Nothing to teach about patterns | Root |

Every one of these is a reasonable thing to add. None of them would have made the pattern
clearer, and several would have obscured it — which is the same judgement Part 4 of the
handbook asks you to apply to the repository itself.
