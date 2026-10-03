# Task 6: Tests and docs

Goal: confidence that the backend is correct and the documentation matches it.

## Steps
1. Core tests (xUnit + FluentAssertions)
   - `Normalizer`: 20 descriptions.
   - `Categorizer`: exact, fuzzy, fallback to `other`.
   - `RecurringDetector`: planted subscriptions, salary, BNPL.
   - `ForecastService`: BNPL persona is `danger`.
   - `OpportunitiesService`, simulations, `NumberValidator` (15 cases, Polish formats, rounding).
2. API tests (`WebApplicationFactory`, in-memory SQLite, fake `ILlmClient`)
   - Demo login, import, dashboard, chat with fake LLM, error format.
3. Docs (keep existing files, do not add new ones)
   - Rewrite `docs/API.md` to the PDF contract.
   - Update `docs/DATA_MODEL.md`, `docs/ARCHITECTURE.md`, `docs/AI_PIPELINE.md` (Gemini, tools, validator).
   - Update `docs/TASKS.md` and `docs/AI_DISCLOSURE.md`.
4. Final checks
   - `cd backend && dotnet build && dotnet test`.
   - Run the API and click through the main flow in Scalar.
   - Confirm `.env` is gitignored and no secrets or real bank data are committed.

## Done when
- Build and all tests pass.
- Docs match the implemented endpoints and DTOs.
- Demo flow works end to end: login, dashboard, wrapped, goals, simulate, chat.
