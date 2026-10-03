# Backend: ASP.NET Core

## Projects
- `src/CashCoach.Api`: minimal API host. `Program.cs` wires DI. Endpoints are grouped in `Endpoints/*Endpoints.cs` as `MapXxxEndpoints(this IEndpointRouteBuilder)` extension methods. DTOs live in `Contracts/`.
- `src/CashCoach.Core`: domain models (`Domain/`), deterministic finance services (`Services/`), interfaces (`Abstractions/`). **No I/O, no HTTP, no AI SDK references.** Pure and unit-testable.
- `src/CashCoach.Infrastructure`: implementations of the Core abstractions:
  - `Import/`: bank CSV parsers (`BankFormatDetector` picks the parser).
  - `Ai/`: `GeminiClient` (thin SDK wrapper), `GeminiCoach : IAiCoach`, `Prompts/*.md`, `Tools/*` (functions Gemini can call).
  - `Persistence/`: EF Core `AppDbContext` on SQLite, repositories.
- `tests/CashCoach.Tests`: xUnit. Every service in `Core/Services` needs tests, and so does every parser.

Dependency direction: `Api → Infrastructure → Core`. Core depends on nothing.

## AI integration: Google Gemini
The project's AI provider is **Gemini** (Google AI Studio API key). Do not add Anthropic, OpenAI or other provider SDKs.

### Keys and where they live
Secrets are in **`.env` at the repo root**. It's gitignored; `.env.example` is the committed template. Never commit `.env`, never print key values, never copy them into `appsettings*.json` or code.

| Variable | Required | Used for |
|---|---|---|
| `GEMINI_API_KEY` | **yes** | Authenticates every Gemini API call |
| `GEMINI_PROJECT_ID` | no | Google Cloud project behind the key. Only needed if we switch to Vertex AI |
| `GEMINI_PROJECT_NAME` | no | Informational (AI Studio project name) |
| `GEMINI_KEY_NAME` | no | Informational (key label in AI Studio) |

### Loading `.env`
ASP.NET Core does not read `.env` by itself. Use the `DotNetEnv` NuGet package in `CashCoach.Api`, and load it **before** `WebApplication.CreateBuilder`, so the values become env vars and flow into `IConfiguration`:
```csharp
DotNetEnv.Env.TraversePath().Load();          // finds ../../.. /.env from the Api project
var builder = WebApplication.CreateBuilder(args);
```
In deployment there's no `.env`. Set the same variables as real environment variables; the code doesn't change.

### Calling Gemini
- SDK: official **`Google.GenAI`** NuGet package (in `CashCoach.Infrastructure`). Don't hand-roll HTTP.
- Register one client in DI, reading the key from `IConfiguration`, and fail fast if it's missing:
  ```csharp
  builder.Services.AddSingleton(sp =>
  {
      var key = sp.GetRequiredService<IConfiguration>()["GEMINI_API_KEY"]
          ?? throw new InvalidOperationException("GEMINI_API_KEY is not set (see .env.example)");
      return new Google.GenAI.Client(apiKey: key);
  });
  ```
- Only `GeminiClient` talks to the SDK. Everything else depends on `IAiCoach` (Core).
- Model name comes from `appsettings.json` → `Gemini:Model` (not secret). Never hardcode it at call sites. Use a current "Flash" model; check AI Studio for the exact name.
- Verify SDK method and type names against the `Google.GenAI` README before writing calls. Don't guess.
- Use **structured output** (response JSON schema) for categorization, insights, savings and affordability. Parse and validate it, and never trust free text for data.
- Use **function calling** for chat. Tools only **read** computed data (`GetTransactionsTool`, `GetCategorySummaryTool`, `CalculateAffordabilityTool`). They never mutate state.
- Check the finish reason (safety block, max tokens) before reading the content. On failure, fall back to templated text.
- Stream chat responses to the frontend over SSE.
- Prompts are loaded from `Ai/Prompts/*.md` (copied to output). Never inline them in C#.
- Free tier: rate limits (429) are likely. Retry with backoff and cache AI results per (month, dataVersion, language).

## Money rules
- `decimal` everywhere. Parse Polish formats: decimal comma `1 234,56`, `;` separators, mBank files may be Windows-1250 encoded.
- Expense < 0, income > 0. Currency PLN. Non-PLN rows are flagged, not converted.
- Before anything goes to Gemini it passes through the anonymizer: drop IBANs and account numbers, names in transfer titles, addresses.

## Conventions
- Nullable enabled, file-scoped namespaces, records for DTOs and value objects.
- Async all the way, with `CancellationToken` on endpoints and services.
- Return `TypedResults` / `Results<...>` from endpoints. Problem Details for errors.
- Config: non-secret settings in `appsettings.json`, secrets from `.env` (via `DotNetEnv`) or real env vars. See "Keys and where they live" above.
- CORS allows `http://localhost:5173` in Development.

## Commands
```bash
dotnet build
dotnet test
dotnet run --project src/CashCoach.Api
dotnet ef migrations add <Name> -p src/CashCoach.Infrastructure -s src/CashCoach.Api
```
