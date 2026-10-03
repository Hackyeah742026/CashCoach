# Tasks

P0 = needed for the demo · P1 = makes it shine · P2 = only if time allows.

## Setup
- [ ] P0 `dotnet new` solution and projects (Api, Core, Infrastructure, Tests) + references
- [ ] P0 `npm create vite` (react-ts) in `frontend/`, add Tailwind, Router, TanStack Query, Recharts
- [ ] P0 `.env.example` → `.env`, `GEMINI_API_KEY` loaded with `DotNetEnv`, `Gemini:Model` config
- [ ] P0 CORS + `/api/health`
- [ ] P0 Extend `.gitignore` for `dist/`, `*.db`, `.env.local`

## Data
- [ ] P0 Synthetic `transactions_mbank.csv` (3 months, about 300 rows, matches the demo persona)
- [ ] P1 Synthetic `transactions_pko.csv`
- [ ] P0 Built-in merchant → category dictionary (about 150 Polish merchants)

## Backend: Core (deterministic)
- [ ] P0 Domain models (`Transaction`, `Category`, `Evidence`, …)
- [ ] P0 `CategorizationService`: normalization + rules + user rules
- [ ] P0 `SpendingAnalysisService`: monthly totals, MoM change, top merchants, facts with keys
- [ ] P0 `RecurringPaymentDetector`
- [ ] P0 `AffordabilityCalculator` (+ installments)
- [ ] P1 `SavingsFinderService`: candidates with computed impact
- [ ] P0 Unit tests for calculator, detector, analysis

## Backend: Infrastructure
- [ ] P0 `CsvTransactionParser` for mBank (`;`, decimal comma, Windows-1250, header offset)
- [ ] P1 PKO BP parser + `BankFormatDetector`
- [ ] P0 SQLite `AppDbContext` + repository
- [ ] P0 `GeminiClient` (`Google.GenAI` wrapper, config, error handling, finish reason checks)
- [ ] P0 Anonymizer
- [ ] P0 AI categorization (structured output)
- [ ] P0 AI spending explanation + fact-checker
- [ ] P1 AI savings phrasing
- [ ] P1 AI affordability explanation
- [ ] P1 Chat with tools + SSE streaming
- [ ] P1 Template fallbacks when AI fails

## Backend: API
- [ ] P0 `POST /transactions/import`, `GET /transactions`, `PATCH /transactions/{id}/category`
- [ ] P0 `GET /insights/summary`
- [ ] P1 `GET /insights/savings`, dismiss
- [ ] P0 `POST /affordability`
- [ ] P1 `POST /chat`
- [ ] P0 `GET/PUT /settings`

## Frontend
- [ ] P0 Theme tokens, layout shell, mobile nav
- [ ] P0 Onboarding: language, payday, upload, import result
- [ ] P0 Dashboard: headline, category chart, recurring list
- [ ] P0 EvidenceDrawer
- [ ] P1 Savings cards (+ dismiss)
- [ ] P0 Afford page: form, verdict card, breakdown, editable assumptions
- [ ] P1 Review queue (low-confidence categories)
- [ ] P1 Chat page with streaming
- [ ] P2 Dark mode, animations, empty states polish

## Submission
- [ ] P0 Fill team name and members in README
- [ ] P0 Pitch deck PDF (≤ 10 slides), see `PITCH_DECK.md`
- [ ] P0 Screenshots for the submission
- [ ] P0 Update `AI_DISCLOSURE.md` with every library actually used
- [ ] P1 Demo video / screen recording (backup)
- [ ] P1 Deploy a demo link
- [ ] P0 Upload to Challenge Rocket
