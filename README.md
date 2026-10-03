# CashCoach

**An AI money coach for young people (18–26) in Poland.** Upload a bank statement and CashCoach explains where your money went, finds concrete savings, and answers *"Can I afford this?"*. Every number it shows can be traced back to your own transactions.

> HackYeah 2026 · Open Task: Artificial Intelligence

| | |
|---|---|
| **Project title** | CashCoach |
| **Team name** | _TBD_ |
| **Team members** | _TBD_ |
| **Demo** | _link TBD_ |
| **Pitch deck** | [`docs/PITCH_DECK.md`](docs/PITCH_DECK.md) → exported PDF (≤ 10 slides) |

---

## The problem

Young adults in Poland are handling money on their own for the first time: a first job on an *umowa zlecenie*, a stipend, rent split between flatmates, BLIK transfers, food delivery, subscriptions, and buy-now-pay-later options such as PayPo or Allegro Pay. Banking apps show a list of transactions and a pie chart. They don't answer the questions people actually ask:

- *Where did my money go this month?*
- *What can I realistically cut, and how much would that save?*
- *Can I afford a 1,200 zł trip in May without running out of money before payday?*

## The solution

CashCoach turns a raw bank CSV into a short, honest conversation about your money.

1. **Import.** Upload a CSV export from your bank (mBank and PKO BP in the MVP). No bank login, nothing stored in the cloud.
2. **Understand.** Transactions are categorized by merchant rules plus AI for the leftovers. Recurring payments and subscriptions are detected automatically.
3. **Explain.** A plain-language monthly summary in Polish or English: what changed, what stood out, what's on autopilot.
4. **Save.** Concrete, numbered suggestions such as *"You ordered from Glovo 14 times (≈ 412 zł). Cooking twice more per week saves ≈ 160 zł/month."*
5. **Decide.** Ask *"Can I afford X?"* and get a green, yellow or red verdict with the full calculation: balance, upcoming bills before payday, safety buffer, and the effect of paying in installments.

## How AI is used, and how you stay in control

**AI explains, code calculates.** All money math (sums, averages, recurring detection, safe-to-spend) is deterministic C# code with unit tests. Claude never invents a number: it gets computed facts and tools, and turns them into explanations, priorities and advice.

| AI does | Code does | User controls |
|---|---|---|
| Categorizes merchants that the rules don't recognize | Parses CSVs and normalizes merchants | Can re-categorize any transaction; the correction becomes a rule |
| Explains spending in plain language | Computes every total and trend | Sees the source transactions behind each claim (Evidence drawer) |
| Ranks and phrases savings ideas | Calculates the monthly impact of each idea | Accepts or dismisses suggestions |
| Answers "can I afford it?" conversationally | Computes the safe-to-spend verdict | Sees and edits assumptions (payday, buffer) |

Every AI answer comes with **evidence**: the transaction IDs and computed figures it is based on. The backend checks AI output against the computed facts before showing it. Details are in [`docs/USER_CONTROL_AND_LIMITATIONS.md`](docs/USER_CONTROL_AND_LIMITATIONS.md).

## Architecture at a glance

```
React (Vite + TS) ──HTTP/SSE──► ASP.NET Core API
                                  ├─ Core: domain + deterministic finance logic
                                  ├─ Infrastructure
                                  │    ├─ Import: bank CSV parsers
                                  │    ├─ Ai: Claude client, prompts, tools
                                  │    └─ Persistence: SQLite (EF Core)
                                  └─► Anthropic Claude API
```

Full description: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) · AI pipeline: [`docs/AI_PIPELINE.md`](docs/AI_PIPELINE.md)

## Tech stack

- **Backend:** .NET (ASP.NET Core minimal APIs), EF Core + SQLite, xUnit
- **AI:** Anthropic Claude API via the official `Anthropic` NuGet SDK. Tool use, structured outputs, streaming.
- **Frontend:** React + TypeScript (Vite), TanStack Query, React Router, plain CSS design tokens, SVG charts

## Getting started

### Prerequisites
- .NET SDK 9+
- Node.js 20+
- An Anthropic API key

### Configuration
```bash
cp .env.example .env
# set ANTHROPIC_API_KEY=...
```

### Run the backend
```bash
cd backend
dotnet restore
dotnet run --project src/CashCoach.Api
# API on http://localhost:5080
```

### Run the frontend
```bash
cd frontend
npm install
npm run dev
# UI on http://localhost:5173
```

### Try it
Upload [`data/samples/transactions_mbank.csv`](data/samples/) on the onboarding screen. The sample data is synthetic.

### Tests
```bash
cd backend && dotnet test
```

## Repository layout

```
backend/     ASP.NET Core API, domain logic, AI integration, tests
frontend/    React web app
data/        synthetic sample bank exports
docs/        architecture, AI pipeline, API, limitations, pitch, demo
```

## Limitations

- Supports CSV import only (no live bank connection yet). The MVP covers mBank and PKO BP.
- Advice is educational, **not** licensed financial advice. CashCoach never recommends specific investment or credit products.
- Categorization of unusual merchants can be wrong. The user can always correct it.

Full list: [`docs/USER_CONTROL_AND_LIMITATIONS.md`](docs/USER_CONTROL_AND_LIMITATIONS.md)

## AI and third-party disclosure

As required by the HackYeah rules, all AI tools, models, APIs and libraries we used are listed in [`docs/AI_DISCLOSURE.md`](docs/AI_DISCLOSURE.md). All code in this repository was written during HackYeah 2026.

## License

_TBD_
