# AI Pipeline

How Google Gemini is used in CashCoach, what it gets as input, what it returns, and how its output is checked.

## Model and settings

| Setting | Value |
|---|---|
| Provider / SDK | Google Gemini API · official `Google.GenAI` NuGet package |
| Model | configurable: `Gemini:Model` |
| Key | `GEMINI_API_KEY` from the root `.env` (loaded with `DotNetEnv`) or an env var in deployment |
| Temperature | 0 for JSON tasks (categorization) · 0.3 for text (insights, savings, affordability, chat) |
| Output | JSON mode with a response schema for every non-chat task. Streaming for chat |
| Tools | Function calling, client-side, read-only |

Model notes:
- Always check the finish reason (safety block, max tokens) before reading the content.
- Keep the system instruction and tool list stable. Volatile data (dates, figures) goes in the user turn.

## The five AI tasks

### 1. Categorize unknown merchants
- **When:** after import, only for transactions the rules left as `Uncategorized`.
- **Input:** batch of `{id, merchant, rawDescription (anonymized), amount, date}` plus the fixed category list.
- **Output (schema):** `[{id, category, confidence: 0–1, normalizedMerchant}]`.
- **Guardrails:** `category` is an enum of our categories. If `confidence < 0.6`, it's shown as "needs review". Users' corrections are stored as rules, so the AI isn't asked again for that merchant.
- **Prompt:** `Prompts/categorize_transactions.md`

### 2. Explain spending ("Where did my money go?")
- **Input:** computed facts from `SpendingAnalysisService`, each with a stable key, e.g. `{"key":"cat.food_delivery.month","value":"412.30"}`, plus top merchants and anomalies.
- **Output (schema):** `{headline, bullets:[{text, factKeys[], transactionIds[]}], tone}`.
- **Guardrails:** fact-check (below). Max 5 bullets. No judgemental language.
- **Prompt:** `Prompts/explain_spending.md`

### 3. Find savings
- **Input:** *candidate* savings from `SavingsFinderService`, each with a computed `monthlyImpact` and evidence.
- **AI role:** pick the 3–5 most relevant ones for this user, phrase them concretely and kindly, and rate their difficulty. It **cannot** create new candidates with new numbers.
- **Output (schema):** `[{candidateId, title, rationale, difficulty: easy|medium|hard}]`. The amounts are joined back from the candidates by ID.
- **Prompt:** `Prompts/find_savings.md`

### 4. "Can I afford this?"
- **Input:** `AffordabilityResult` from `AffordabilityCalculator` (verdict, safe-to-spend, breakdown, assumptions).
- **AI role:** a 2–3 sentence explanation and 1–3 practical tips (e.g. "buy after payday on the 10th", "skip 2 deliveries"). It **cannot** change the verdict.
- **Prompt:** `Prompts/can_i_afford.md`

### 5. Chat coach
- Free-form questions ("How much did I spend on Bolt in March?", "Can I save 500 zł for summer?").
- **Tools:**

| Tool | Purpose | Returns |
|---|---|---|
| `get_transactions` | Filter by date range, category, merchant | list (max 50) + total |
| `get_category_summary` | Totals per category for a period | computed sums |
| `calculate_affordability` | Run the deterministic calculator for a hypothetical purchase | `AffordabilityResult` |

- Tool results are collected and returned to the UI as evidence for the answer.
- **Prompt:** `Prompts/system_coach.md` (persona, language, safety rules)

## Fact-checking layer

After every AI response, before it reaches the user:

1. Extract every money amount from the text (regex for Polish and English formats: `412,30 zł`, `PLN 412.30`, `412 zł`).
2. Each amount must match (±0.01, or ±1 zł when the text says "około" or "≈") a value from the facts or tool results given to the model in that request.
3. If the check fails, retry once with an error note. If it fails again, fall back to a templated, non-AI sentence built from the facts.
4. Log failures (count only) to show reliability in the demo.

## Privacy: anonymizer

Runs before any data is sent to Gemini:
- removes IBANs and account numbers (`\d{26}`, `PL\d{26}`), card numbers, phone numbers;
- strips personal names from transfer titles ("Przelew od JAN KOWALSKI" becomes "Przelew od [osoba]");
- sends merchant, category, amount, date and transaction ID only. Balances are sent only for affordability.

## Prompt files

| File | Used by |
|---|---|
| `system_coach.md` | all tasks: persona, language, safety rules, "never invent numbers" |
| `categorize_transactions.md` | task 1 |
| `explain_spending.md` | task 2 |
| `find_savings.md` | task 3 |
| `can_i_afford.md` | task 4 |

Persona: friendly, direct, non-judgemental older-sibling tone. Answers in the user's language (PL or EN). Never recommends specific financial products, loans or investments. Adds a gentle nudge to seek professional help when it detects debt stress signals (repeated BNPL, overdraft).

## Cost and latency (estimate)
- Import of about 300 transactions: 1 categorization call (only the unknown merchants).
- Dashboard: 2 calls (insight and savings), cached per month and data version.
- Fallback: if the API is unavailable, the app still works and shows the computed numbers with templated text.
