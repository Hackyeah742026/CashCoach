# Task 1: Create the project

Goal: an empty but building .NET 9 solution with all projects, references and NuGet packages.

## Steps
Run from `backend/`:
```
dotnet new sln -n CashCoach
dotnet new classlib -n CashCoach.Core -o src/CashCoach.Core -f net9.0
dotnet new classlib -n CashCoach.Infrastructure -o src/CashCoach.Infrastructure -f net9.0
dotnet new web -n CashCoach.Api -o src/CashCoach.Api -f net9.0
dotnet new xunit -n CashCoach.Core.Tests -o tests/CashCoach.Core.Tests -f net9.0
dotnet new xunit -n CashCoach.Api.Tests -o tests/CashCoach.Api.Tests -f net9.0
dotnet sln add src/CashCoach.Core src/CashCoach.Infrastructure src/CashCoach.Api tests/CashCoach.Core.Tests tests/CashCoach.Api.Tests
```

References:
- Infrastructure -> Core
- Api -> Core, Infrastructure
- Core.Tests -> Core
- Api.Tests -> Api

NuGet packages (versions compatible with .NET 9, EF Core 9.x):
- Core: `FuzzySharp`
- Infrastructure: `Microsoft.EntityFrameworkCore.Sqlite`, `Google.GenAI`, `Bogus`, `CsvHelper`
- Api: `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, `DotNetEnv`
- Tests: `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing` (Api.Tests), SQLite in-memory or `Microsoft.EntityFrameworkCore.InMemory`

Also:
- Create the folder layout from the plan (`Domain/`, `Services/`, `Abstractions/`, `Persistence/`, `Ai/Prompts/`, `Data/`, `Endpoints/`, `Contracts/`).
- Create `backend/CLAUDE.md` (referenced by the root `CLAUDE.md`).
- Make sure `.env.example` has `GEMINI_API_KEY`, `VITE_API_URL` placeholders.
- Add every new library to `docs/AI_DISCLOSURE.md`.

## Done when
- `dotnet build` passes with no warnings about missing references.
- `dotnet test` runs (empty tests pass).
- All packages are listed in `docs/AI_DISCLOSURE.md`.
