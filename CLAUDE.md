# CashCoach

AI money coach for young people (18–26) in Poland. Imports bank CSVs, explains spending, finds savings, answers "can I afford this?". This is a HackYeah 2026 hackathon project (Open Task: AI). The judges weigh innovation, fit with the AI category, usability, design and completeness.

## Repo map
- `backend/`: ASP.NET Core API (.NET 9). See `backend/CLAUDE.md`.
- `frontend/`: React + TS + Vite. See `frontend/CLAUDE.md`.
- `docs/`: architecture, AI pipeline, API contract, data model, limitations, pitch, demo, tasks.
- `data/samples/`: **synthetic** bank CSVs for dev and demo.

Read `docs/ARCHITECTURE.md` and `docs/API.md` before changing anything that crosses the backend/frontend boundary.

## AI provider: Google Gemini
- The product's AI is **Google Gemini**, called from the backend only via the official `Google.GenAI` .NET SDK. The frontend never talks to Gemini and never sees a key.
- The key is **`GEMINI_API_KEY`**, stored in **`.env` at the repo root** (gitignored). `.env.example` is the committed template with the variable names:
  - `GEMINI_API_KEY`: required
  - `GEMINI_PROJECT_ID`, `GEMINI_PROJECT_NAME`, `GEMINI_KEY_NAME`: optional, informational
  - `VITE_API_URL`: frontend → backend base URL
- The backend loads `.env` with `DotNetEnv` at startup and reads values through `IConfiguration`. In deployment, the same names are real env vars.
- Details on how to call it: `backend/CLAUDE.md` → "AI integration: Google Gemini".

## Core principle: AI explains, code calculates
- Every money figure (sums, averages, safe-to-spend, savings impact) is computed by deterministic, unit-tested C# in `CashCoach.Core`. Gemini must never be the source of a number.
- Gemini gets computed facts via function calls or prompt context and returns explanations, rankings and phrasing.
- Every AI output carries `evidence` (transaction IDs and figure keys). The backend validates that every amount the AI mentions matches a computed figure before returning it.
- The user can override everything: categories, assumptions (payday, buffer), suggestions.

## Conventions
- Money is `decimal` in C# and integer grosze or string in JSON. Never `float`/`double`. Currency is PLN.
- Expenses are negative amounts, income positive.
- UI copy is Polish and English. AI answers in the user's selected language.
- Keep the API contract in `docs/API.md` in sync when you change endpoints or DTOs.
- Prompts live as `.md` files in `backend/src/CashCoach.Infrastructure/Ai/Prompts/`. Never inline them in C#.

## Safety and privacy
- Never commit real bank data. `data/samples/` must stay synthetic.
- Before sending anything to Gemini, strip account numbers, IBANs, personal names in transfer titles, and addresses. Send merchant, category, amount and date only.
- No investment, credit or specific-product recommendations. Coaching only. Keep the "not financial advice" disclaimer in the UI.
- **Secrets:** only in `.env` (local) or env vars (deploy). Never commit `.env`, never hardcode or log key values, never put them in `appsettings*.json`, docs or frontend code. If you add a new variable, add it to `.env.example` with a placeholder value.

## Commands
```bash
cp .env.example .env            # then put the real GEMINI_API_KEY in .env
cd backend && dotnet build && dotnet test
cd backend && dotnet run --project src/CashCoach.Api
cd frontend && npm run dev
cd frontend && npm run build && npm run lint
```

## Working style for agents
- Hackathon pace: the simplest thing that demos well and is correct. No speculative abstractions.
- Prioritize from `docs/TASKS.md` (P0 before P1).
- When you add a dependency, add it to `docs/AI_DISCLOSURE.md` (the rules require disclosing libraries, APIs and models).
- Don't create new docs files unless asked. Update the existing ones.
