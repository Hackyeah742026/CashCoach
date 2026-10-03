# Backend (ASP.NET Core, .NET 9)

Read the root `CLAUDE.md` first. API contract: `docs/API.md`.

## Layout
```
backend/
  CashCoach.sln
  src/
    CashCoach.Core            domain, services, abstractions (no I/O)
    CashCoach.Infrastructure  EF Core + SQLite, Gemini client, CSV import, seed data, prompts
    CashCoach.Api             minimal API endpoints, contracts (DTOs), Program.cs
  tests/
    CashCoach.Core.Tests      unit tests for calculations
    CashCoach.Api.Tests       endpoint tests via WebApplicationFactory
```
References: Infrastructure → Core; Api → Core + Infrastructure; Core.Tests → Core; Api.Tests → Api.

## Layering rules
- `Core` has **no I/O**: no EF, HTTP, file system or Gemini. It defines interfaces in `Abstractions/` that Infrastructure implements.
- Endpoints in `Api/Endpoints/` only parse, call services, and map to DTOs in `Api/Contracts/`. Register everything in DI from `Program.cs`.
- Constructor injection, `async` + `CancellationToken` for I/O, options pattern for settings.

## Money
- `decimal` everywhere in C#, never `float`/`double`. Currency PLN.
- Stored as integer **grosze** in the DB; JSON as numbers in zł with 2 decimals (Kasa Coach contract).
- Expenses negative, income positive. All figures are computed in `Core`, unit-tested.

## AI integration: Google Gemini
- Use the official `Google.GenAI` SDK, in `Infrastructure/Ai/` only, behind an interface from `Core/Abstractions`.
- Key: `GEMINI_API_KEY` from the root `.env`, loaded at startup with `DotNetEnv` (`Env.TraversePath().Load()`) and read via `IConfiguration`. Never log or hardcode it.
- Prompts are `.md` files in `Infrastructure/Ai/Prompts/` (mark as `CopyToOutputDirectory` or embedded when first used), never inline strings.
- Gemini explains; code calculates. Anonymize data before sending (merchant, category, amount, date only). Validate that amounts in AI output match computed figures.

## Commands
```bash
cd backend
dotnet build
dotnet test
dotnet run --project src/CashCoach.Api
```
OpenAPI document at `/openapi/v1.json`, Scalar UI at `/scalar/v1` (`/scalar` redirects there; Development environment only). The API listens on `http://localhost:5080`.

SDK: projects target `net9.0` but are built with the .NET 10 SDK (no `global.json`). The local 9.0.304 SDK has a broken workload manifest; run `dotnet workload repair` before pinning SDK 9.

## Tests
xUnit + FluentAssertions. Core tests are pure; API tests use `WebApplicationFactory<Program>` (Program is `public partial`) with a SQLite in-memory database. No test may call the real Gemini API.
