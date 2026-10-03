# AI Pipeline

How Google Gemini is used in CashCoach, what it gets, what it returns, and how its output is checked. Code: `backend/src/CashCoach.Infrastructure/Ai/` and `backend/src/CashCoach.Core/Ai/`.

## Model and settings

| Setting | Value |
|---|---|
| SDK | Official `Google.GenAI` NuGet package, behind `ILlmClient` (`Core/Abstractions`) |
| Model | `GEMINI_MODEL`, default `gemini-3.5-flash-lite`: about 2 s per chat answer and a usable free tier. `gemini-3.8-flash` allows only 20 free requests a day; `gemini-2.5-flash` is closed to new keys |
| Key | `GEMINI_API_KEY` from the root `.env` (`DotNetEnv`) or an env var. Never logged |
| Temperature | 0.3 for text, 0 for JSON tasks |
| Output limit | 400 tokens for chat, 1024 otherwise. Thinking level `low` on Gemini 3 models |
| Timeout / retry | 15 s per call; one retry on network errors, timeouts and 5xx. 4xx (quota, bad request) is not retried |
| No key, quota, outage | `LlmUnavailableException` → every feature falls back to deterministic templates |

## The AI tasks

### 1. Categorize unknown merchants (`GeminiCategorizer`)
- **When:** on import, only for normalized merchant keys the dictionary and fuzzy match (FuzzySharp ≥ 85) could not resolve. Up to 50 keys per call.
- **Input:** a JSON array of keys, e.g. `["PHU POLMAX"]`. Keys from bank-transfer titles are **never sent** (they may contain personal names) and become `other`.
- **Output (JSON mode):** `{ "KEY": "category" }`. Unknown categories are ignored; anything unanswered stays `other`. Answers are learned into the in-memory dictionary.
- **Prompt:** `Prompts/categorize.md`.

### 2. Chat coach (`ChatAgent` + `ToolRegistry`)
- **Input:** system prompt `Prompts/chat_system.md` (`{today}` = latest transaction date, `{lang_name}`), the last 10 messages of the conversation, and the question with account numbers, cards, phones and e-mails scrubbed (`Anonymizer`).
- **Tools** (the user id is injected by the server; the model never chooses it):

| Tool | Returns (computed in Core) |
|---|---|
| `get_balance` | balance, safe-to-spend, safe per day, buffer, bills before payday, next payday, days left |
| `get_spending` | totals by category and merchant for a month or range, up to 20 transactions (date, merchant, category, amount) |
| `get_upcoming_payments` | bills, subscriptions, rent, BNPL instalments before payday |
| `forecast_until_payday` | projected balance on payday, run-out date, status, typical daily spending |
| `simulate_purchase` | verdict green/yellow/red, safe-to-spend, left after / shortfall, forecast after, goal delays |
| `simulate_change` | weekly/monthly spending now, monthly and yearly saving, goal reach dates |
| `list_subscriptions` | subscriptions, monthly total, duplicates |
| `get_goals` | goal progress, status, required per week, reach date |
| `create_goal` | creates a goal (only when asked) |
| `get_savings_opportunities` | the computed savings ideas |

- **Loop:** up to 5 tool rounds, then the model must answer. Tool results go back as function responses; Gemini's own content (thought signatures) is passed back unchanged.
- **Output:** plain text in the user's language plus evidence (transaction ids and labelled figures from the tools). Stored in `chat_messages` with the facts.

### 3. Wrapped captions and opportunity explanations (`AiCopywriter`)
- **Input:** the computed facts (JSON, zł) and the template texts by id. Prompt `Prompts/captions.md`.
- **Output (JSON mode):** `{ "id": "rewritten text" }`. Each text is fact-checked against the facts and its own template; a failing text keeps the template. Cached per user, month (or opportunity set) and language for 6 hours (2 minutes after a failure).

## Fact checking (`NumberValidator`)

Runs on every AI text before it reaches the user:

1. Remove dates and times (`2026-10-15`, `15.10.2026`, `15 października`, `October 15`, `18:30`, `2026-09`).
2. Extract every number in Polish or English format: `1 234,50` (space or NBSP groups), `1,234.50`, `1234.5`, `24.`, `30%`. An ambiguous `1,234` is accepted as either 1234 or 1.234; `3 120` may also be read as `3` and `120`.
3. Ignore whole numbers 0 to 10 (counts) and years 1900 to 2100.
4. Each remaining number must match a fact (absolute value): ±0.01 for decimals, ±1 for whole numbers (rounded złoty). Facts are every number in the tool results (plus day, month and year of ISO dates in them) and the numbers the user typed.
5. **Chat:** on failure, the model gets a correction naming the unverified numbers and tries again (max 2 retries). Then a template answer built from the forecast is returned with `fact_check: "fallback"`. **Copy:** a failing text keeps its template. Every failure is logged with the unverified numbers.

## Privacy

- Sent to Gemini: merchant names, categories, amounts and dates (as tool results or aggregates), merchant keys for categorization, and the user's own chat text after scrubbing.
- Never sent: raw bank descriptions, transfer titles, IBANs and account numbers, the user's name, the user id.
- `Anonymizer` removes IBANs (`PL61 1090 …`, 26 digits), runs of 9+ digits, phone numbers and e-mail addresses.

## Safety (system prompt)
Coaching only: no investment, credit, loan or specific product recommendations; never suggests taking new debt or BNPL; does not ask for personal data. Tested live: "Polecisz mi jakąś kartę kredytową albo kredyt?" gets a refusal plus an offer of budgeting help.

## Prompt files

| File | Used by |
|---|---|
| `chat_system.md` | chat: persona, language, number rules, tool guide, safety |
| `categorize.md` | merchant categorization |
| `captions.md` | Wrapped captions and opportunity explanations |

Prompts are embedded resources (`CashCoach.Infrastructure.csproj`), loaded with `PromptLibrary`.

## Cost and latency
- Import of about 200 transactions: at most 1 categorization call (only unknown merchants).
- Dashboard, forecast, simulations, goals: no model call.
- Wrapped and opportunities: 1 call each, then cached.
- Chat: typically 2 to 3 calls per question (tool call, answer), about 2 s with `gemini-3.5-flash-lite`. The free Gemini tier allows only a few requests per minute, so quota errors fall back to templates.
- Tests use `FakeLlmClient`; no test calls Gemini.
