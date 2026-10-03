# Tasks

P0 = needed for the demo · P1 = makes it shine · P2 = only if time allows.

## Setup
- [x] P0 Solution and projects (Api, Core, Infrastructure, Core.Tests, Api.Tests) + references
- [x] P0 `npm create vite` (react-ts) in `frontend/`, add Router, TanStack Query, lucide-react
- [x] P0 `.env.example` → `.env`, `GEMINI_API_KEY` (+ optional `GEMINI_MODEL`) via `DotNetEnv` and `IConfiguration`
- [x] P0 CORS + `/api/health`
- [x] P0 `.gitignore` for `.env`, `*.db`

## Data
- [x] P0 Synthetic CSVs for 3 personas (`data/samples/synthetic_*.csv`, fixed seed)
- [x] P0 Built-in merchant → category dictionary (about 150 Polish merchants)

## Backend: Core (deterministic)
- [x] P0 Domain models, normalizer, categorizer (dictionary, fuzzy, LLM fallback), recurring detector, BNPL plans
- [x] P0 Spending summary, subscriptions overview
- [x] P0 `ForecastCalculator` (payday, fixed upcoming, median daily spend, run-out date, safe-to-spend)
- [x] P1 `OpportunityFinder` (delivery, duplicate/unused subscriptions, small buys, taxi, BNPL)
- [x] P0 `Simulator` (purchase verdict, category change), `GoalCalculator`/`GoalPlanner`
- [x] P1 `WrappedBuilder` (8 cards), `AlertBuilder`, `ChallengeTracker`, PL/EN templates
- [x] P0 `NumberValidator`, `Anonymizer`
- [x] P0 Unit tests (121)

## Backend: Infrastructure
- [x] P0 CSV reader (`;`, decimal comma or dot) + import service
- [x] P0 SQLite `AppDbContext` (schema version, recreate on change)
- [x] P0 `GeminiClient` (`Google.GenAI`, timeout, retry, no-key fallback)
- [x] P0 AI categorization (JSON mode, transfer titles never sent)
- [x] P1 Chat agent with 10 tools, fact check, retries, template fallback
- [x] P1 AI Wrapped captions and opportunity explanations (fact-checked, cached)
- [ ] P1 PKO BP / mBank native export formats + `BankFormatDetector`

## Backend: API
- [x] P0 Demo login, `/me` (settings, consent, export, delete), import, transactions (+ `ids` filter), summary, subscriptions, BNPL
- [x] P0 `/dashboard`, `/forecast`, `/opportunities` (+ dismiss), `/simulate/purchase`, `/simulate/change`, `/alerts` (+ dismiss)
- [x] P1 `/wrapped`, `/wrapped/months`, `/goals` (+ deposit, preview), `/challenges` (+ check-in)
- [x] P1 `POST /chat` (JSON or SSE), `GET /chat/{id}`, `GET /chat/suggestions`, rate limit
- [x] P0 API tests (69, fake LLM)
- [x] P0 Connect the frontend (see `frontend/docs/BACKEND_INTEGRATION.md`)
- [x] P0 Income detection and confirmation in onboarding, balance from CSV (see `docs/INCOME_DETECTION.md`)

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
