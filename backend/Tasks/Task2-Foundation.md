# Task 2: Foundation (PDF B1, B2)

Goal: a running API with health check, OpenAPI + Scalar, error format, database and user resolution.

## Steps
1. `Program.cs`
   - Load root `.env` with `DotNetEnv`.
   - CORS for the Vite origin.
   - JSON snake_case via `System.Text.Json` naming policy.
   - `AddOpenApi()` / `MapOpenApi()` (`/openapi/v1.json`) and `MapScalarApiReference()` (`/scalar/v1`), dev only.
   - `MapGroup("/api")` and `GET /api/health`.
2. Error handling
   - `ApiException(code, message, statusCode)`.
   - Global handler returns `{ "error": { "code": "...", "message": "..." } }`.
3. Database (`CashCoach.Infrastructure/Persistence`)
   - `AppDbContext` with SQLite, `EnsureCreated` on startup.
   - Entities (amounts in integer grosze, `long AmountGr`):
     - `User` (id, name, persona, language, consent_at, created_at)
     - `Transaction` (id, user_id, date, amount_gr, raw_description, merchant, category, channel, is_recurring, recurring_group_id, is_bnpl)
     - `RecurringGroup` (id, user_id, merchant, type, avg_amount_gr, period_days, next_date, active, user_confirmed)
     - `Goal` (id, user_id, name, emoji, target_gr, saved_gr, deadline, monthly_plan_gr, created_at)
     - `Challenge` (id, user_id, type, title, start_date, end_date, target, progress, streak, status)
     - `ChatMessage` (id, user_id, conversation_id, role, content, facts_json, created_at)
   - Enums: `Category` (15 fixed values), `Channel`, `RecurringType`, `Persona`.
4. `X-User-Id`
   - Endpoint filter resolves it into a `CurrentUser` service.
   - Missing header or unknown user returns an `ApiException` (401 / 404).

## Done when
- `dotnet run --project src/CashCoach.Api` starts and `GET /api/health` returns 200.
- Scalar opens at `/scalar/v1` and shows the health endpoint.
- All 6 tables exist in the SQLite file.
- A thrown `ApiException` returns the documented error JSON.
