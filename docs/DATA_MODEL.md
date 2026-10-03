# Data Model

Entities live in `backend/src/CashCoach.Core/Domain/`. SQLite via EF Core (`Infrastructure/Persistence/AppDbContext.cs`), created with `EnsureCreated`. Tables and columns are `snake_case`; enums are stored as `snake_case` strings. Money is stored as integer **grosze** (`long …Gr`); expenses negative, income positive.

There are no migrations: `AppDbContext.SchemaVersion` is written to SQLite's `user_version`, and a database from an older version is dropped and recreated on startup (it only holds demo data).

## Tables

### `users`
| Column | Type | Notes |
|---|---|---|
| `id` | GUID | Demo personas have fixed ids (`de000000-…-000000000001` to `…03`) |
| `name`, `persona`, `language` | text | `persona`: `student` · `first_job` · `bnpl_heavy`; `language`: `pl` · `en` |
| `consent_at`, `created_at` | datetime | |
| `payday` | int? | Day of month the salary arrives; detected on import, user-editable |
| `safety_buffer_gr` | long | Default 30 000 (300 zł) |
| `balance_gr` | long? | Balance at the latest transaction date. CSVs carry no balance, so it is set by the user (or per demo persona); when `null` it is estimated from the history |

### `transactions`
`id`, `user_id`, `date`, `amount_gr`, `raw_description` (never sent to the AI), `merchant` (display name, e.g. `Glovo`), `category`, `channel` (`card` · `blik` · `transfer`), `is_recurring`, `recurring_group_id`, `is_bnpl`. Duplicates (same user, date, amount, description) are skipped on import.

### `recurring_groups`
`id`, `user_id`, `merchant`, `type` (`subscription` · `rent` · `salary` · `bnpl`), `avg_amount_gr` (signed), `period_days` (30 or 7), `next_date`, `active`, `user_confirmed` (the "still using it?" answer, `null` until answered). Rebuilt after every import; ids and answers survive by merchant + type.

### `goals`
`id`, `user_id`, `name`, `emoji`, `target_gr`, `saved_gr`, `deadline`, `monthly_plan_gr`, `created_at`. Progress, status and reach date are computed on read (`GoalCalculator`).

### `challenges`
`id`, `user_id`, `type` (`no_delivery` · `no_taxi`), `title`, `start_date`, `end_date`, `target` (days), `progress` (best streak), `streak`, `status` (`active` · `completed` · `failed`), `last_break_date` (latest breaking payment already counted), `last_check_in_on`.

### `chat_messages`
`id`, `user_id`, `conversation_id`, `role` (`user` · `assistant`), `content` (user text is scrubbed of account numbers first), `facts_json` (assistant only: tools used, transaction ids, figures, `fallback`, `fact_check`), `created_at`.

### `user_merchant_rules`
`user_id`, `merchant`, `category`. Created by `PATCH /transactions/{id}` with `apply_to_merchant`; applied on later imports.

### `dismissals`
`user_id`, `key`, `created_at`. Keys are `alert:<alert id>` or `opportunity:<opportunity id>`; both ids are stable strings such as `run_out:2026-10-05` or `duplicate_subscription:music`.

## Computed (not stored)
All in `CashCoach.Core/Analytics/`, built from a `FinancialSnapshot` (as-of date, balance, payday, buffer, transactions, active recurring groups, goals):

| Type | From | Notes |
|---|---|---|
| `Forecast` | `ForecastCalculator` | Next payday, fixed upcoming payments, median daily spending, projected end, run-out date, status, safe-to-spend, daily series |
| `Opportunity` | `OpportunityFinder` | Stable id, type, monthly/yearly saving, monthly spend, count, difficulty, transaction ids |
| `PurchaseSimulation`, `ChangeSimulation` | `Simulator` | Before/after forecasts, verdict, goal impacts |
| `GoalProgress`, `GoalPreview` | `GoalCalculator`, `GoalPlanner` | Status, required per week/month, short-by, reach date; preview verdict and plan |
| `WrappedStats` | `WrappedBuilder` | Monthly totals, top categories/merchant, delivery, biggest day, subscriptions, month-over-month, cheapest weekday, fun equivalent, personality |
| `Alert` | `AlertBuilder` | `run_out`, `bnpl`, `duplicate_sub`, `challenge` |

Evidence attached to API output: `{ transaction_ids: Guid[], figures: { key, label, amount }[] }`.
