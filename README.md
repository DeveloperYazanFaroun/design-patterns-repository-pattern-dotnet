# Design Patterns &amp; the Repository Pattern

A runnable reference application for the training session of the same name. Every topic in
the handbook has working code here, and every piece of code points back at the handbook
section it came from.

**Stack:** C# 14 · .NET 10 · EF Core 10 · ASP.NET Core Minimal APIs · xUnit

> Prepared by **Yazan Faroun**.

---

## Contents

- [What is in here](#what-is-in-here)
- [Quick start](#quick-start)
- [Solution layout](#solution-layout)
- [The dependency rule](#the-dependency-rule)
- [Pattern index — all 23](#pattern-index--all-23)
- [The repository stack](#the-repository-stack)
- [API surface](#api-surface)
- [Database](#database)
- [Tests](#tests)
- [Docs](#docs)
- [Push it to a remote](#push-it-to-a-remote)
- [Troubleshooting](#troubleshooting)

---

## What is in here

| Learning objective | Where it lives |
|---|---|
| **1.** Recognise the three pattern categories | `src/PatternsLab.Patterns/` — 23 runnable demos, one file each |
| **2.** Explain the Repository Pattern's intent and structure | `src/PatternsLab.Domain/Abstractions/` + `src/PatternsLab.Infrastructure/Repositories/` |
| **3.** Implement a generic repository with a Unit of Work | `Repository<T>`, `Specification<T>`, `UnitOfWork` |
| **4.** Judge when it adds value and when it adds layers | `docs/DECISIONS.md` + the anti-pattern notes in the code |

Two things this project deliberately does **not** do, because the handbook argues against
them: it never returns `IQueryable<T>` from a repository, and no repository ever calls
`SaveChanges`.

---

## Quick start

```bash
# 1. clone / open the folder
cd Design-Patterns-and-the-Repository-Pattern

# 2. restore + build
dotnet restore
dotnet build

# 3. run the tests (no database needed — the integration ones use SQLite in-memory)
dotnet test

# 4. run the API
dotnet run --project src/PatternsLab.Api
```

A browser opens on <http://localhost:5080/swagger> — every route with a description and a
**Try it out** button, so you can exercise the API without writing a single curl. The root
endpoint <http://localhost:5080> lists everything else.

On the first run the app creates `patternslab.db` (SQLite) next to the API project and
seeds five claims and three patients, so every endpoint returns something interesting
immediately.

**Try these first:**

```bash
curl http://localhost:5080/api/patterns                      # the whole GoF catalogue
curl -X POST http://localhost:5080/api/patterns/decorator/run  # watch a pattern run
curl http://localhost:5080/api/claims/outstanding/PBM-01     # the named business rule
curl http://localhost:5080/api/audit                         # what the observers recorded
```

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.100** or later | `dotnet --version`. Pinned in `global.json` with `rollForward: latestFeature`. |
| SQL Server | optional | Only if you switch the provider — SQLite is the default and needs nothing installed. |
| EF Core tools | optional | `dotnet tool install --global dotnet-ef` — only needed if you add migrations. |

---

## Solution layout

```
Design-Patterns-and-the-Repository-Pattern/
├── PatternsLab.sln
├── Directory.Build.props          ← TargetFramework + package versions, in ONE place
├── global.json                    ← pins the SDK
├── README.md
├── docs/
│   ├── Design-Patterns-and-the-Repository-Pattern.pdf   ← the session handbook
│   ├── EXERCISES.md               ← the trainee workbook
│   ├── SOLUTIONS.md               ← worked answers (don't peek)
│   └── DECISIONS.md               ← why this codebase is shaped the way it is
├── src/
│   ├── PatternsLab.Domain/        ← entities, specifications, repository INTERFACES
│   │   ├── Common/                   Entity, AggregateRoot, domain events, Result
│   │   ├── Abstractions/             IRepository<T>, ISpecification<T>, IUnitOfWork
│   │   ├── Claims/                   the Claim aggregate + its specifications
│   │   └── Patients/                 the Patient aggregate
│   ├── PatternsLab.Application/   ← use cases; owns transaction boundaries
│   │   ├── Claims/                   ClaimSubmissionService, facade, event handlers
│   │   └── Pricing/                  PricingService (Strategy consumer)
│   ├── PatternsLab.Infrastructure/← the ONLY project that references EF Core
│   │   ├── Persistence/              AppDbContext, SpecificationEvaluator, UnitOfWork
│   │   ├── Repositories/             Repository<T>, EfClaimRepository, + decorators
│   │   ├── Events/                   DomainEventDispatcher
│   │   ├── External/                 adapters and stubs for third-party services
│   │   └── DependencyInjection/      the composition root and a hand-rolled Decorate()
│   ├── PatternsLab.Patterns/      ← the 23 GoF patterns, standalone and runnable
│   │   ├── Creational/               5 patterns
│   │   ├── Structural/               7 patterns
│   │   └── Behavioural/              11 patterns
│   └── PatternsLab.Api/           ← minimal APIs, seeding, the background worker
└── tests/
    ├── PatternsLab.UnitTests/         no database, milliseconds
    └── PatternsLab.IntegrationTests/  SQLite in-memory, real SQL
```

---

## The dependency rule

```
        Api  ──────────────┐
         │                 │
         ▼                 ▼
    Application ─────► Infrastructure
         │                 │
         ▼                 │
      Domain  ◄────────────┘
   (references nothing)
```

Calls go **down**. The implementation dependency goes **up**: `Infrastructure` implements
interfaces that `Domain` declares. That inversion is the pattern — get it backwards
(interfaces in the infrastructure project) and you have a folder rename, not an
abstraction. See handbook §2.5, Figure 2.2.

You can prove the rule holds:

```bash
# should print nothing at all
grep -r "EntityFrameworkCore" src/PatternsLab.Domain src/PatternsLab.Application
```

---

## Pattern index — all 23

Every one is a single file, and every one is runnable:
`POST /api/patterns/{key}/run`.

### Creational — *how objects get made* (`src/PatternsLab.Patterns/Creational/`)

| Key | Pattern | What the demo shows |
|---|---|---|
| `abstract-factory` | Abstract Factory | A provider family that can never be mixed |
| `builder` | Builder | A claim that cannot escape half-built |
| `factory-method` | Factory Method | Adding a channel without touching the service |
| `prototype` | Prototype | `with` expressions, and when a deep copy is needed |
| `singleton` | Singleton | Hand-rolled vs. container-managed, and captive dependencies |

### Structural — *how objects are composed* (`src/PatternsLab.Patterns/Structural/`)

| Key | Pattern | What the demo shows |
|---|---|---|
| `adapter` | Adapter | An XML vendor SDK behind a clean interface, exceptions included |
| `bridge` | Bridge | 2 report types × 3 renderers without 6 classes |
| `composite` | Composite | A charge tree totalled without a single `if (isGroup)` |
| `decorator` | Decorator | Logging → Caching → Retry → the real reader |
| `facade` | Facade | Four collaborators behind one call |
| `flyweight` | Flyweight | 30 000 lines, 3 shared objects |
| `proxy` | Proxy | Lazy creation and role-based refusal |

### Behavioural — *how objects talk and decide* (`src/PatternsLab.Patterns/Behavioural/`)

| Key | Pattern | What the demo shows |
|---|---|---|
| `chain-of-responsibility` | Chain of Responsibility | Approval routed by amount |
| `command` | Command | Undo that works for every command ever written |
| `interpreter` | Interpreter | A tiny claim-filter grammar, parsed and evaluated |
| `iterator` | Iterator | `yield return`, and laziness you can watch |
| `mediator` | Mediator | 3 links instead of 6 |
| `memento` | Memento | A snapshot the caretaker cannot read |
| `observer` | Observer | Subscribe, notify, unsubscribe |
| `state` | State | Transitions each state owns itself |
| `strategy` | Strategy | The switch that stopped growing |
| `template-method` | Template Method | A fixed pipeline with pluggable steps |
| `visitor` | Visitor | Two operations, zero edits to the nodes |

Several of these also appear **in production form** inside the application:

| Pattern | Production use |
|---|---|
| Strategy | `IPricingRule` → `PricingService` |
| Observer | `AggregateRoot.Raise` → `UnitOfWork` → `IDomainEventDispatcher` |
| Decorator | `LoggingClaimRepository` → `CachingClaimRepository` → `EfClaimRepository` |
| Adapter | `EmailNotifierAdapter` over `LegacySmtpGateway` |
| Factory Method | `NotifierFactory` |
| Facade | `ClaimSubmissionFacade` |
| Template Method | `OutstandingClaimChaser : BackgroundService` |
| Interpreter | `Specification<T>` — a query described as an object tree |
| Visitor | `ParameterRebinder : ExpressionVisitor` inside `Specification.AndAlso` |
| Memento | EF Core's own change tracker |

---

## The repository stack

Read the files in this order — it is the order the handbook builds them in Part 3:

1. `Domain/Common/Entity.cs` — `Entity`, `AggregateRoot`, the audit/soft-delete markers
2. `Domain/Abstractions/IRepository.cs` — deliberately small; note what is **missing**
3. `Domain/Abstractions/ISpecification.cs` + `Specification.cs` — queries as objects
4. `Domain/Claims/Specifications/ClaimSpecifications.cs` — the named business rules
5. `Infrastructure/Persistence/SpecificationEvaluator.cs` — the one place spec meets EF Core
6. `Infrastructure/Repositories/Repository.cs` — the mechanics
7. `Infrastructure/Repositories/EfClaimRepository.cs` — the vocabulary
8. `Infrastructure/Persistence/UnitOfWork.cs` — commit, audit, soft delete, events
9. `Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` — lifetimes
10. `Application/Claims/ClaimSubmissionService.cs` — the payoff: no EF Core in sight

### The rules this codebase enforces

| Rule | Where you can see it |
|---|---|
| Repositories never return `IQueryable<T>` | `IRepository<T>` has no such method |
| Repositories never call `SaveChanges` | only `UnitOfWork` does |
| One repository per **aggregate root** | there is no `IClaimLineRepository` |
| Query rules have names | `OutstandingClaimsSpec`, not `Where(...)` in a controller |
| Cross-cutting write concerns live once | `UnitOfWork.SaveChangesAsync` |
| Everything persistence-related is **scoped** | see the composition root's comments |
| Background work opens its own scope | `OutstandingClaimChaser` |

---

## API surface

| Method | Route | Shows |
|---|---|---|
| `GET` | `/` | Index of everything |
| `GET` | `/api/patterns?category=Structural` | The GoF catalogue (Table 1.1, live) |
| `GET` | `/api/patterns/{key}` | One pattern's card |
| `POST` | `/api/patterns/{key}/run` | Run a demo, get a narrated transcript |
| `POST` | `/api/patterns/run-all` | Run all 23 — a decent smoke test |
| `GET` | `/api/claims` | Search through a specification |
| `GET` | `/api/claims/summaries` | A projection, not whole aggregates |
| `GET` | `/api/claims/{id}` | One claim with its lines |
| `GET` | `/api/claims/outstanding/{payer}` | The named business rule |
| `GET` | `/api/claims/deleted` | Soft-delete opt-out |
| `POST` | `/api/claims` | Two aggregates, one transaction |
| `POST` | `/api/claims/{id}/reprice?planType=tiered` | Strategy selected at run time |
| `POST` | `/api/claims/{id}/pay` | Raises a domain event |
| `DELETE` | `/api/claims/{id}` | Becomes a soft delete |
| `GET` | `/api/reports/totals-by-payer` | Database-side aggregation |
| `GET` | `/api/patients` | Patient ids to POST against |
| `GET` | `/api/pricing/plans` | The Strategy registry |
| `GET` | `/api/audit` | What the domain-event observers recorded |
| `GET` | `/health` | Connectivity + active provider |

### Trying the API

Three ways, all Development-only:

| Where | What it gives you |
|---|---|
| <http://localhost:5080/swagger> | Swagger UI — every route, its summary, and a **Try it out** form. Opens automatically on `dotnet run`. |
| `src/PatternsLab.Api/PatternsLab.Api.http` | Ready-made requests for VS Code (REST Client), Visual Studio and Rider. The claim flow chains its ids, so `POST /api/claims` picks up a real `patientId` from the patients call above it. |
| `/openapi/v1.json` | The raw document, for Postman, Insomnia, `openapi-generator` or anything else that eats OpenAPI. |

The `.WithTags()` and `.WithSummary()` calls on each endpoint are what make the Swagger page
readable — they are metadata on the route, so the document stays in step with the code
instead of drifting from a hand-written spec.

Swagger is mapped inside `if (app.Environment.IsDevelopment())`: a deployed API should not
hand strangers an executable map of itself.

### Creating a claim

```bash
# grab a patient id first
curl http://localhost:5080/api/patients

curl -X POST http://localhost:5080/api/claims \
  -H "Content-Type: application/json" \
  -d '{
        "patientId": "PASTE-A-GUID-HERE",
        "number": "CLM-2001",
        "serviceDate": "2026-09-01",
        "payerCode": "PBM-01",
        "lines": [
          { "procedureCode": "A4253", "quantity": 100 },
          { "procedureCode": "E0607", "quantity": 1 }
        ]
      }'
```

Post it twice and the second attempt returns **409 Conflict** with a message that names
the duplicated field — the error style the handbook argues for.

---

## Database

SQLite is the default so `dotnet run` works on a clean machine with nothing installed.

### Switching to SQL Server

```jsonc
// src/PatternsLab.Api/appsettings.Development.json
{
  "Database": { "Provider": "SqlServer" },
  "ConnectionStrings": {
    "SqlServer": "Server=.;Database=PatternsLab;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

or run the ready-made profile:

```bash
dotnet run --project src/PatternsLab.Api --launch-profile sqlserver
```

The provider changes **one** behaviour, and the code says so in both places: on SQL Server
the concurrency token is a real `rowversion` maintained by the database; on SQLite the
Unit of Work stamps it. See `AppDbContext.ApplyConcurrencyTokens` and
`UnitOfWork.StampConcurrencyTokens`.

### Migrations

The sample calls `EnsureCreatedAsync()` for a frictionless first run. A real project uses
migrations instead:

```bash
dotnet tool install --global dotnet-ef

dotnet ef migrations add InitialCreate \
  --project src/PatternsLab.Infrastructure \
  --startup-project src/PatternsLab.Api

dotnet ef database update \
  --project src/PatternsLab.Infrastructure \
  --startup-project src/PatternsLab.Api
```

Then replace `EnsureCreatedAsync()` with `MigrateAsync()` in `Program.cs`. (The two do not
mix: a database made by `EnsureCreated` has no migrations history.)

Delete `patternslab.db` any time you want a clean seed.

---

## Tests

```bash
dotnet test                                              # everything
dotnet test tests/PatternsLab.UnitTests                  # no I/O, milliseconds
dotnet test tests/PatternsLab.IntegrationTests           # SQLite in-memory, real SQL
dotnet test --filter "FullyQualifiedName~Specification"  # one slice
```

| Suite | What it proves | Speed |
|---|---|---|
| `Domain/ClaimTests` | Aggregate invariants, with no test doubles at all | instant |
| `Domain/SpecificationTests` | Business rules, by compiling the criteria | instant |
| `Application/ClaimSubmissionServiceTests` | Service decisions, with a substituted `IUnitOfWork` | instant |
| `Patterns/*` | All 23 patterns behave as described | instant |
| `IntegrationTests/RepositoryTests` | Specifications translate and return the right rows | ~1s |
| `IntegrationTests/UnitOfWorkTests` | Atomicity, audit, soft delete, concurrency | ~1s |

**Why SQLite and not `Microsoft.EntityFrameworkCore.InMemory`:** the InMemory provider is
not a relational database — no joins, no constraints, no transactions, different null and
ordering semantics. Queries that pass there routinely fail against SQL Server, so testing
against it gives false confidence. Handbook §3.11.

---

## Docs

| File | For |
|---|---|
| `docs/Design-Patterns-and-the-Repository-Pattern.pdf` | The 42-page session handbook |
| `docs/EXERCISES.md` | The trainee workbook — 18 exercises across all four objectives |
| `docs/SOLUTIONS.md` | Worked answers and marking notes |
| `docs/DECISIONS.md` | Why this codebase is shaped the way it is, including what was deliberately left out |

---

## Push it to a remote

The repository is not initialised yet. From the project folder:

```bash
git init
git branch -M main
git add .
git commit -m "Design Patterns & the Repository Pattern — training reference application"
```

Then create an empty remote repository (no README, no .gitignore — this project has both)
and push:

```bash
# GitHub
git remote add origin https://github.com/<you>/design-patterns-repository-pattern.git
git push -u origin main

# Azure DevOps
git remote add origin https://dev.azure.com/<org>/<project>/_git/<repo>
git push -u origin main
```

With the GitHub CLI it is one command:

```bash
gh repo create design-patterns-repository-pattern --private --source=. --remote=origin --push
```

### Before you push

- `.gitignore` already excludes `bin/`, `obj/`, `*.db` and `appsettings.*.local.json`.
- Nothing in `appsettings.json` is a secret — the SQL Server string uses integrated auth.
  If you point it at a real server, move the connection string to user secrets:
  ```bash
  dotnet user-secrets set "ConnectionStrings:SqlServer" "<real connection string>" \
    --project src/PatternsLab.Api
  ```
- `docs/*.pdf` is committed on purpose so a trainee gets the handbook with the clone.

### Suggested branch for the lab

Have each trainee work on their own branch so you can diff their answers:

```bash
git checkout -b lab/<their-name>
```

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `The current .NET SDK does not support targeting .NET 10.0` | Install the .NET 10 SDK, or lower `TargetFramework` in `Directory.Build.props`. |
| `SQLite Error 1: 'no such table: Claims'` | Delete `patternslab.db` and rerun — the schema changed since it was created. |
| A claim you edited is not saved | You loaded it through a read-only specification. See the `tracked` parameter on `OutstandingClaimsSpec`. |
| `ObjectDisposedException` on `DbContext` in a worker | Something captured a scoped service in a singleton. See `OutstandingClaimChaser`. |
| Page 2 of a list shows the same total as page 1 | `CountAsync` applied paging. See `SpecificationEvaluator.Apply(forCount: true)`. |
| `Cannot decorate IClaimRepository` at startup | `Decorate<>` ran before the concrete registration. Order matters in the composition root. |
| Concurrency test fails on SQL Server but not SQLite | Expected — the token is generated in different places. See `ApplyConcurrencyTokens`. |

---

## One sentence to take away

> Patterns are named trade-offs — reach for one when you can name the change it makes
> cheap, and delete it when you can't.
