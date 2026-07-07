# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

.NET 10 backend of dmoney-tracker (personal money tracker, Vietnamese-first). Sibling repos: `../dmoney-tracker-web` (React frontend, expects this API at http://localhost:5113) and `../dmoney-tracker-orchestrator` (full-stack docker compose). Design specs live in the archived monorepo: github.com/datnm555/dmoney-tracker (`docs/superpowers/`).

## Commands

```bash
dotnet build DMoney.slnx                 # zero-warnings gate (TreatWarningsAsErrors + AnalysisMode=All)
dotnet test DMoney.slnx                  # all suites; integration tests REQUIRE Docker running (Testcontainers)
dotnet test tests/Application.UnitTests  # fast unit loop, no Docker needed
dotnet test tests/Application.UnitTests --filter FullyQualifiedName~MoneyTests   # single class
dotnet run --project src/Web.Api         # http://localhost:5113, auto-migrates in Development
dotnet tool restore                      # once, for dotnet-ef
dotnet dotnet-ef migrations add <Name> --project src/Infrastructure --startup-project src/Web.Api --output-dir Database/Migrations
```

Dev DB: run postgres via the orchestrator repo (`docker compose up -d postgres` from `../dmoney-tracker-orchestrator`) or any postgres on localhost:5432 with db `dmoney`, user/pass `postgres`/`postgres`.

Migrations MUST use `--output-dir Database/Migrations` (EF's default `Migrations/` is wrong for this repo). Migration files are analyzer-exempt via `.editorconfig`.

## Architecture

Five projects with a compiler-enforced dependency rule — `tests/ArchitectureTests` (NetArchTest) fails the build if violated: `SharedKernel` ← `Domain` ← `Application` ← `Infrastructure` ← `Web.Api`. Application may reference EF Core but not ASP.NET; Infrastructure must not reference ASP.NET either (this is why `UserContext` lives in Web.Api and why `Pbkdf2PasswordHasher` uses only BCL crypto).

**Custom CQRS, no MediatR.** Commands/queries are records implementing `ICommand<T>`/`IQuery<T>`; handlers are `internal sealed` classes named `*CommandHandler`/`*QueryHandler` (architecture tests enforce both), returning `Result<T>` from SharedKernel — never throwing for domain failures. Each handler is registered explicitly in `Application/DependencyInjection.cs`.

**Error flow:** domain/handlers return `Error` objects with stable codes (e.g. `Transactions.EmptyAmount`). `Web.Api/Middleware/ResultExtensions.ToHttpResult(localizer)` maps `ErrorType` → HTTP status (Validation→400, NotFound→404, Conflict→409, Unauthorized→401) and localizes the description by looking up `Error.Code` in the resx. Error descriptions in C# are English neutral fallbacks; translations live only in resx.

**Endpoints** are one-class-per-route minimal-API classes implementing `IEndpoint`, auto-discovered from the assembly (`AddEndpoints`/`MapEndpoints`). Protected routes call `.RequireAuthorization()` and use the localized `ToHttpResult` overload.

**Auth invariant:** `AddJwtBearer` sets `MapInboundClaims = false` and `UserContext` reads the raw `"sub"` claim — these two are a pair; changing one breaks the other. Per-user isolation is done in query predicates (`t.UserId == userId`); operations on another user's records return 404, never 403. Handlers take `IUserContext` and check `UserId is not { } userId` first.

**Persistence:** EF Core + Npgsql, no repository layer — handlers use `IApplicationDbContext` directly. `Money` (Domain value object, VND-only for now) maps as an owned type to paired columns (`credit_amount`/`credit_currency`, same for debit). `Money.Zero()` is a factory method, not a shared instance — EF owned types need distinct instances per owner. `AuditingInterceptor` stamps `CreatedAt`/`ModifiedAt` on `AuditedEntity`. Aggregations (monthly summary, dashboard stats) are SQL-side `GroupBy`/`Sum` — don't materialize raw rows. Current schema snapshot: `docs/database-schema.md`.

**i18n:** `Web.Api/Resources/SharedResource.{vi,en}.resx` is the single translation source for BOTH backend error messages and ALL frontend labels. `GET /resources?lang=vi|en` serves the whole dictionary (output-cached 1h, vary by lang). Culture comes from `?lang=` query (first) or Accept-Language; default `vi`. When adding any user-facing string, add the key to BOTH resx files — the frontend's `t()` falls back silently to raw keys, so a missing key will not fail any test.

**Cross-repo contract:** the category codes in `Domain/Transactions/TransactionCategories.cs` are comment-synced with `src/utils/categories.ts` in the web repo; payment method / card type codes in `Domain/Transactions/PaymentMethods.cs` + `CardTypes.cs` are comment-synced with `src/utils/paymentMethods.ts` in the web repo; response DTO shapes (camelCased by ASP.NET) are mirrored in the web repo's `src/api/types.ts`. Change both sides together.

**Middleware order in Program.cs matters:** `UseCors` must come before `UseOutputCache` (cached `/resources` responses must carry CORS headers). Config keys `Cors:Origins` (`;`-separated) and `Database:AutoMigrate` exist for the Docker deployment; dev defaults preserve local behavior.

## Testing conventions

- Unit tests mock `IApplicationDbContext` with `MockQueryable.NSubstitute` (`.BuildMockDbSet()`) — async EF operators (ToListAsync, SumAsync, GroupBy) work against in-memory lists. Time-dependent logic injects `IDateTimeProvider` and pins the clock in tests.
- Integration tests use `ApiTestFactory` (`WebApplicationFactory` + a Postgres Testcontainer per test class, migrations applied). Authenticated flows register+login real users via the API; use unique emails per test. Date-sensitive tests derive dates from `DateTime.UtcNow` so rolling windows always contain them.
