# Architecture

## Overview

```
┌─────────────────────────┐        ┌──────────────────────────────────────────────┐
│  Frontend (React + TS)  │  HTTP  │  CashCoach.Api (ASP.NET Core minimal APIs)   │
│  Onboarding · Dashboard │◄──────►│  /transactions /insights /affordability /chat│
│  Afford · Chat          │  SSE   └───────────────┬──────────────────────────────┘
│  EvidenceDrawer         │                        │
└─────────────────────────┘        ┌───────────────▼──────────────┐
                                   │  CashCoach.Core (pure C#)    │
                                   │  Domain + deterministic math │
                                   │  Categorization rules        │
                                   │  SpendingAnalysis            │
                                   │  RecurringPaymentDetector    │
                                   │  SavingsFinder               │
                                   │  AffordabilityCalculator     │
                                   └───────────────▲──────────────┘
                                                   │ implements abstractions
                                   ┌───────────────┴──────────────┐
                                   │  CashCoach.Infrastructure    │
                                   │  Import: CSV parsers         │
                                   │  Ai: GeminiCoach + tools     │──► Google Gemini API
                                   │  Persistence: EF Core/SQLite │
                                   └──────────────────────────────┘
```

## Components

### Frontend
Single-page React app, mobile-first. It holds no business logic: it renders what the API computes and streams chat. `EvidenceDrawer` is the shared component that shows the transactions and calculation behind any claim.

### CashCoach.Api
Thin HTTP layer: validation, DTO mapping, SSE streaming, CORS, Problem Details. Endpoints delegate to Core services and to `IAiCoach`.

### CashCoach.Core
The trusted center of the system. Pure C#, no I/O, fully unit-tested.

| Service | Responsibility |
|---|---|
| `CategorizationService` | Merchant normalization, then a rule dictionary (Biedronka → Groceries, Glovo → Food delivery, …) and user-defined rules. Leaves unknown merchants as `Uncategorized` for the AI step. |
| `RecurringPaymentDetector` | Finds subscriptions and regular bills: same merchant, similar amount, period of about 7, 14, 30 or 365 days. |
| `SpendingAnalysisService` | Monthly totals per category, month-over-month deltas, top merchants, anomalies (a category above 1.5× its 3-month average). |
| `SavingsFinderService` | Produces *candidate* savings with computed monthly impact: unused or duplicate subscriptions, high-frequency small purchases, delivery vs. groceries ratio, BNPL usage. |
| `AffordabilityCalculator` | `safeToSpend = balance − upcoming recurring until payday − buffer`. Handles one-off and installment purchases and returns a verdict plus a line-by-line breakdown. |

### CashCoach.Infrastructure
- **Import:** `BankFormatDetector` sniffs headers and encoding and picks a parser. `CsvTransactionParser` handles Polish number and date formats. Output is a list of `Transaction`.
- **Ai:** `GeminiClient` wraps the `Google.GenAI` SDK (model, temperature, streaming, error handling). `GeminiCoach` implements `IAiCoach`: categorize, explain, find savings, affordability narrative, chat. Prompts are `.md` files and tools are classes in `Tools/`. See [AI_PIPELINE.md](AI_PIPELINE.md).
- **Persistence:** EF Core + SQLite (single file, zero setup). Stores transactions, user category rules, settings (payday, buffer, language) and dismissed suggestions.

## Key flows

### 1. Import
```
CSV upload → BankFormatDetector → CsvTransactionParser → Transactions
  → CategorizationService (rules)            ~80–90% categorized
  → GeminiCoach.Categorize(unknown merchants) structured output + confidence
  → RecurringPaymentDetector
  → save → return ImportResult (counts, uncategorized left, date range)
```

### 2. Monthly insight
```
SpendingAnalysisService → computed facts (JSON, each with a stable key)
  → GeminiCoach.ExplainSpending(facts) → narrative + evidence keys
  → FactChecker: every amount in the narrative must match a fact key (±0.01)
  → InsightDto
```

### 3. "Can I afford this?"
```
AffordabilityRequest (item, price, date, installments?)
  → AffordabilityCalculator → verdict + breakdown + assumptions (deterministic)
  → GeminiCoach.ExplainAffordability(result) → short explanation + 1–3 tips
  → AffordabilityResponse (the verdict always comes from the calculator)
```

### 4. Chat
Gemini with function calling (`GetTransactions`, `GetCategorySummary`, `CalculateAffordability`), streamed over SSE. Tool results are attached to the reply as evidence.

## Technical decisions

| Decision | Why |
|---|---|
| **AI explains, code calculates** | LLMs are unreliable at arithmetic. Deterministic math makes the outputs verifiable and testable, which answers the brief's "how can users verify outputs?" question. |
| Rules first, AI for the long tail | Cheaper, faster and deterministic for known merchants. AI only handles what the rules miss, and user corrections become new rules. |
| CSV import, not open banking | No PSD2 licence is needed for a hackathon, and it's privacy-friendly. Open banking (e.g. via an AIS provider) is the natural next step. |
| SQLite | Zero setup and a single file, so the demo is portable. |
| Clean layering (Core has no dependencies) | Finance logic is unit-testable and the AI provider is swappable behind `IAiCoach`. |
| Gemini via the official `Google.GenAI` .NET SDK | Function calling, JSON outputs and streaming are first-class. Strong multilingual quality (Polish). |
| Anonymize before AI | GDPR/RODO: only merchant, category, amount and date leave the server. |

## Deployment (demo)
- Backend: a single container or `dotnet run`. SQLite file in a volume.
- Frontend: static build served by the API (`wwwroot`) or any static host.
- Secrets: `GEMINI_API_KEY` via env var (root `.env` locally).
