# Architecture

## Overview

```
┌─────────────────────────┐  HTTP + SSE  ┌──────────────────────────────────────────────────┐
│  Frontend (React + TS)  │◄────────────►│  CashCoach.Api (ASP.NET Core minimal APIs)       │
│  Home · Wrapped · Chat  │  X-User-Id   │  /dashboard /forecast /opportunities /simulate/* │
│  Goals · EvidenceDrawer │              │  /wrapped /goals /challenges /alerts /chat …     │
└─────────────────────────┘              └───────────────┬──────────────────────────────────┘
                                                         │
                                         ┌───────────────▼──────────────────┐
                                         │  CashCoach.Core (pure C#, no I/O)│
                                         │  Domain · Services (import math) │
                                         │  Analytics (forecast, savings,   │
                                         │   simulations, goals, wrapped,   │
                                         │   alerts, challenges, templates) │
                                         │  Ai (NumberValidator, Anonymizer)│
                                         │  Abstractions (ILlmClient, …)    │
                                         └───────────────▲──────────────────┘
                                                         │ implements
                                         ┌───────────────┴──────────────────┐
                                         │  CashCoach.Infrastructure        │
                                         │  Import (CSV) · Categorization   │
                                         │  Persistence (EF Core + SQLite)  │
                                         │  Analytics (load snapshot, CRUD) │
                                         │  Ai: GeminiClient, ChatAgent,    │──► Google Gemini API
                                         │   ToolRegistry, AiCopywriter,    │    (Google.GenAI SDK)
                                         │   GeminiCategorizer, Prompts/*.md│
                                         └──────────────────────────────────┘
```

## Projects

### CashCoach.Api
Endpoints only parse and validate input, call services and map to DTOs (`Contracts/`). Cross-cutting: snake_case JSON, CORS for the Vite origin, `{ error: { code, message } }` errors, `X-User-Id` resolution (`CurrentUserFilter`), a 20 requests/minute per-user rate limit on `/chat`, OpenAPI + Scalar in Development.

### CashCoach.Core
The trusted center. Pure, deterministic, unit-tested.

| Area | Types |
|---|---|
| Import math | `MerchantNormalizer`, `Categorizer` (dictionary, fuzzy, LLM fallback), `RecurringDetector`, `BnplPlanBuilder`, `SpendingSummaryCalculator`, `SubscriptionOverviewCalculator` |
| Analytics | `FinancialSnapshot`, `ForecastCalculator`, `OpportunityFinder`, `Simulator`, `GoalCalculator`/`GoalPlanner`, `WrappedBuilder`, `AlertBuilder`, `ChallengeTracker`, `CoachTexts` (PL/EN templates) |
| AI safety | `NumberValidator` (every number in AI text must match a computed fact), `Anonymizer` (account numbers, cards, phones, e-mails) |
| Abstractions | `ILlmClient`, `ILlmCategorizer`, `IMerchantDictionary` |

### CashCoach.Infrastructure
- **Import:** `CsvTransactionReader` (`;`, decimal comma or dot) → `TransactionImportService` (normalize, categorize, user rules, recurring sync, payday detection).
- **Persistence:** `AppDbContext` on SQLite.
- **Analytics:** `SnapshotLoader` (one read per request), `AnalyticsService` (dismissals applied), `GoalService`, `ChallengeService`, `DismissalService`, `UserDataService` (export, delete).
- **Ai:** see [AI_PIPELINE.md](AI_PIPELINE.md).
- **SyntheticData:** fixed-seed Bogus generator for the three demo personas (`data/samples/`).

## Key flows

### Demo login and import
```
POST /demo/login → create persona user (fixed id, demo balance)
  → synthetic CSV → TransactionImportService
      normalize → dictionary / fuzzy → Gemini for unknown merchants (not transfer titles) → other
      → user rules → save → RecurringSyncService → payday from salary
```

### Dashboard
```
SnapshotLoader → ForecastCalculator, OpportunityFinder (− dismissed), AlertBuilder, WrappedBuilder (month totals),
GoalCalculator, SubscriptionOverviewCalculator, BnplPlanBuilder → DashboardResponse (templates only, no model call)
```

### Wrapped and opportunities
```
WrappedBuilder / OpportunityFinder (numbers) → template captions (CoachTexts)
  → AiCopywriter: Gemini rewrites, NumberValidator checks each text, failures keep the template
  → cached per user, month/version and language
```

### Chat
```
POST /chat → ChatAgent: history (last 10) + scrubbed question → Gemini with 10 tools
  → tool calls run Core code for this user (max 5 rounds) → facts collected
  → NumberValidator: passed → answer; failed → retry with a correction (max 2) → template fallback
  → stored with facts → JSON or SSE (tool, delta, evidence, done)
```

## Technical decisions

| Decision | Why |
|---|---|
| **AI explains, code calculates** | LLMs are unreliable at arithmetic. Deterministic math is verifiable and testable; the validator enforces it at runtime. |
| "Today" = latest transaction date | Imported histories are analysed as of their end, and the demo tells the same story every day. |
| Templates first, AI on top | Every screen works without a key or when Gemini is down or over quota; AI only rephrases verified facts. |
| Rules first, AI for the long tail | Dictionary and fuzzy match are cheap and deterministic. User corrections become rules. |
| CSV import, not open banking | No PSD2 licence needed; privacy-friendly. |
| SQLite, `EnsureCreated` + schema version | Zero setup, portable demo. |
| Gemini via the official `Google.GenAI` SDK | Function calling and JSON mode; strong Polish. Model configurable with `GEMINI_MODEL` (default `gemini-3.5-flash-lite`). |
| Anonymize before AI | GDPR/RODO: only merchant, category, amount and date leave the server. |

## Deployment (demo)
- Docker (whole stack): `docker compose up --build` at the repo root runs `backend` (`backend/Dockerfile`) and `web` (`frontend/Dockerfile`: Vite build served by nginx on http://localhost:8080). nginx proxies `/api/*` to the backend unbuffered (chat SSE), so the browser uses one origin and the web image is built with `VITE_API_URL=/api`, `VITE_USE_MOCKS=false`. Secrets come from the root `.env` via `env_file`; SQLite is in the `cashcoach-data` volume.
- Backend: `dotnet run --project src/CashCoach.Api` (port 5080) locally. Deployed as a container from `backend/Dockerfile` on Render (`render.yaml` Blueprint, free plan). It listens on `$PORT` (default 8080), and SQLite lives at `/data/cashcoach.db`, which is ephemeral on the free plan (demo personas re-seed on login).
- Frontend: Vite static build on Vercel (https://cash-coach-one.vercel.app, root `frontend/`, `vercel.json` rewrites routes to `index.html`). Build env: `VITE_API_URL=https://<backend-host>/api`, `VITE_USE_MOCKS=false`.
- CORS: `Cors:AllowedOrigins` in `appsettings.json`, overridable with `Cors__AllowedOrigins__0..n` env vars.
- Secrets: `GEMINI_API_KEY` (and optional `GEMINI_MODEL`) via env vars; root `.env` locally.
