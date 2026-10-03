# API Contract

Base URL: `http://localhost:5080/api`. OpenAPI: `/openapi/v1.json`, Scalar UI: `/scalar/v1` (Development only).

## Conventions
- **JSON:** `snake_case` property names and enum values (`food_delivery`, `on_track`).
- **Money:** JSON **numbers** in złoty with 2 decimals (`-11.00`). Stored as integer grosze. Expenses are negative, income positive. Fields named `monthly_saving`, `safe_to_spend`, `amount` of a payment etc. are positive unless documented otherwise. Requests reject amounts with more than 2 decimals.
- **Dates:** ISO `YYYY-MM-DD`. Months are `YYYY-MM`.
- **"Today":** analytics run **as of the user's latest transaction date** (`as_of`), so the demo data (Jul to Sep 2026) always tells the same story. `GET /me` returns `as_of`.
- **Errors:** `{ "error": { "code": "…", "message": "…" } }` with a matching HTTP status. Unexpected failures: `500 internal_error`. Malformed JSON: `400 bad_request`.
- **User:** every endpoint except `GET /health` and `POST /demo/login` needs the `X-User-Id` header (user GUID). Missing: `401 missing_user_id`. Not a GUID: `400 invalid_user_id`. Unknown: `404 user_not_found`.
- **Evidence:** numbers and AI text carry `evidence: { transaction_ids: [...], figures: [{ key, label, amount }] }`. `label` is in the user's language. Load the transactions with `GET /transactions?ids=a,b,c`.
- **AI text:** fields with an `ai_generated` flag were written by Gemini and passed the number fact check; otherwise they are deterministic templates. Every number comes from C# in `CashCoach.Core`.

Categories: `groceries`, `food_delivery`, `restaurants`, `transport`, `subscriptions`, `shopping`, `entertainment`, `rent`, `utilities`, `health`, `education`, `bnpl`, `transfers`, `salary`, `other`.

---

## System
`GET /health` → `{ "status": "ok" }`

## Users

### `POST /demo/login`
No `X-User-Id`. Body `{ "persona": "student" | "first_job" | "bnpl_heavy" }`. Creates the persona's demo user on first login (fixed id per persona) and imports its synthetic CSV. Returns the profile below. Store `user_id` and send it as `X-User-Id`.

### `POST /users`
No `X-User-Id`. Body `{ "name"?: "Ola", "language"?: "pl" | "en" }` → `201` with the profile below (`persona: "custom"`, `has_data: false`). The frontend creates this user at the start of onboarding, then sets assumptions and imports a CSV or demo data into it.

### `GET /me`
```json
{
  "user_id": "de000000-0000-0000-0000-000000000003",
  "name": "Maja",
  "persona": "bnpl_heavy",
  "language": "pl",
  "has_consent": false,
  "has_data": true,
  "payday": 15,
  "safety_buffer": 300.00,
  "balance": 2100.00,
  "balance_is_estimate": false,
  "as_of": "2026-09-30",
  "available_months": ["2026-07", "2026-08", "2026-09"]
}
```
`payday` is detected from the salary on import (user-editable). `balance` is the value set by the user; when unset it is estimated from the imported history (`balance_is_estimate: true`), because CSV exports carry no balance.

### `PATCH /me`
Any subset of `{ "name", "language": "pl"|"en", "payday": 1-31, "safety_buffer": 300.00, "balance": 1601.52 }`. Returns the profile. Errors: `invalid_name`, `invalid_language`, `invalid_payday`, `invalid_safety_buffer`, `invalid_balance`.

### `POST /me/consent`
Body `{ "accepted": true, "scopes": ["transactions", "ai_coach"] }` → `{ "consent_at": "2026-10-03T12:00:00+00:00", "scopes": [...] }`. `accepted: false` → `400 consent_not_accepted`.

### `GET /me/export`
Downloads everything stored about the user as JSON (`Content-Disposition: attachment`): `user`, `transactions`, `recurring_groups`, `goals`, `challenges`, `chat_messages`, `merchant_rules`, `dismissals`. Amounts here are raw grosze (`amount_gr`).

### `DELETE /me`
Deletes the user and all their data. `204`.

## Transactions

### `POST /import`
Multipart field `file` (max 5 MB). CSV header `date;amount;description;currency`, `;` delimiter, decimal comma or dot, PLN only. Duplicate rows (same user, date, amount and description) are skipped. After the import, recurring payments are re-detected and `payday` is set from the salary if it was unknown.
```json
{
  "imported": 212,
  "skipped_duplicates": 0,
  "categorized": { "dictionary": 190, "fuzzy": 14, "llm": 4, "other": 4 },
  "recurring_found": { "subscriptions": 4, "bnpl": 1, "salary_day": 28 },
  "period": { "from": "2026-07-01", "to": "2026-09-30" }
}
```
Errors: `invalid_csv`, `file_too_large`.

### `POST /import/demo`
Body `{ "persona": "student" | "first_job" | "bnpl_heavy" }` (default `bnpl_heavy`). Loads the persona's synthetic history into the **current** user ("try it with demo data"). Same response as `POST /import`. The payday follows the demo salary; the persona's balance is used unless the user already set one.

### `GET /transactions?from=&to=&category=&q=&ids=&limit=50&offset=0`
`q` matches merchant or raw description. `ids` is a comma-separated list of transaction ids (the Evidence drawer); bad ids → `400 invalid_ids`. `total` is the number of matches (not money).
`sum` is the signed złoty sum of all matching transactions (before paging).
```json
{
  "total": 12,
  "sum": -466.80,
  "items": [{
    "id": "…", "date": "2026-08-14", "amount": -38.90, "merchant": "Glovo", "category": "food_delivery",
    "raw_description": "GLOVO*ORDER 123 KRAKOW", "channel": "card", "is_recurring": false, "is_bnpl": false
  }]
}
```

### `PATCH /transactions/{id}`
Body `{ "category": "restaurants", "apply_to_merchant": true }` → `{ "id": "…", "category": "restaurants", "updated_count": 6 }`. With `apply_to_merchant` the choice becomes a rule for future imports. Errors: `404 transaction_not_found`, `400 invalid_category`.

## Insights

### `GET /summary?period=this_month|last_month|last_3_months`
Calendar months relative to `as_of`. `total_spent` is positive; `share` is a fraction; `vs_prev_pct` is the change against the previous period of the same length (`null` when nothing was spent then).
```json
{
  "period": { "from": "2026-09-01", "to": "2026-09-30" },
  "total_spent": 6052.53,
  "categories": [{
    "category": "rent", "amount": 2300.00, "count": 1, "share": 0.38, "vs_prev_pct": 0.0,
    "top_merchants": [{ "merchant": "Czynsz", "amount": 2300.00, "count": 1 }]
  }]
}
```

### `GET /subscriptions`
```json
{
  "monthly_total": 84.97,
  "items": [{ "id": "…", "merchant": "Spotify", "amount": 23.99, "next_date": "2026-10-06", "group": "music", "duplicate": true, "user_confirmed": null }]
}
```
`group` is `music`, `video` or `null`; `duplicate` is true when another active subscription is in the same group.

### `PATCH /subscriptions/{id}`
Body `{ "still_using": false }` → `{ "id": "…", "user_confirmed": false, "new_opportunity_id": "unused_subscription:…" }`. `new_opportunity_id` is set when the user no longer uses it (the saving appears in `GET /opportunities`). Unknown id: `404 subscription_not_found`.

### `GET /bnpl`
```json
{
  "active_plans": 3, "total_remaining": 3266.94,
  "items": [{ "provider": "Klarna", "merchant": "Zalando", "instalment": 99.99, "paid": 4, "total": 6, "remaining": 199.98, "next_date": "2026-10-07" }],
  "explainer": "Masz 3 aktywne plany…"
}
```
`explainer` is a fixed template.

## Analytics

### `GET /forecast`
Day-by-day projection until the next payday.
```json
{
  "as_of": "2026-09-30",
  "next_payday": "2026-10-15",
  "days_left": 15,
  "balance": 2100.00,
  "fixed_upcoming": 2080.97,
  "daily_variable": 117.22,
  "projected_end": -1739.27,
  "run_out_date": "2026-10-05",
  "status": "danger",
  "safety_buffer": 300.00,
  "safe_to_spend": -280.97,
  "safe_per_day": 0.00,
  "upcoming": [{ "date": "2026-10-01", "merchant": "Czynsz", "type": "rent", "amount": 1600.00 }],
  "series": [{ "date": "2026-09-30", "balance": 2100.00 }, { "date": "2026-10-01", "balance": 382.78 }],
  "evidence": { "transaction_ids": ["…"], "figures": [{ "key": "forecast.safe_to_spend", "label": "Bezpiecznie do wydania", "amount": -280.97 }] }
}
```
- `fixed_upcoming`: active subscriptions, rent and the next BNPL instalment due after `as_of` and before payday.
- `daily_variable`: median daily non-recurring spending over the last 30 days.
- `projected_end` = `balance − fixed_upcoming − daily_variable × days_left`; `run_out_date` is the first simulated day below zero.
- `status`: `ok` (≥ 200 zł left on payday), `tight` (0 to 200 zł), `danger` (below zero).
- `safe_to_spend` = `balance − fixed_upcoming − safety_buffer` (may be negative); `safe_per_day` = max(0, safe) / days left.

### `GET /opportunities`
Top 5 savings ideas (dismissed ones excluded), largest first. Rules: `food_delivery` (half of delivery spend), `duplicate_subscription` (all but the priciest in a music/video group), `unused_subscription` (marked "not using"), `small_daily_buys` (30% of buys ≤ 25 zł), `taxi_rides` (half of Bolt/Uber/FreeNow), `bnpl` (instalments freed after payoff). Monthly figures average the last 90 days.
```json
{
  "total_monthly_saving": 616.05,
  "total_yearly_saving": 7392.60,
  "items": [{
    "id": "food_delivery",
    "type": "food_delivery",
    "title_key": "opportunity.food_delivery",
    "title": "Zamawiaj jedzenie o połowę rzadziej",
    "rationale": "Zamawiając jedzenie rzadziej, w rok odłożysz aż 3 889,92 zł! 🍔",
    "monthly_saving": 324.16,
    "yearly_saving": 3889.92,
    "difficulty": "medium",
    "evidence": { "transaction_ids": ["…"], "figures": [{ "key": "opportunity.monthly_spend", "label": "Wydatki miesięcznie", "amount": 648.32 }] },
    "ai_generated": true
  }]
}
```

### `POST /opportunities/{id}/dismiss`
`204`. The opportunity stops appearing (by its stable `id`).

### `POST /simulate/purchase`
Body `{ "amount": 400.00, "date": "2026-10-02", "item": "Buty", "assumptions": { "payday": 10, "safety_buffer": 300.00, "balance": 1601.52 } }`. `date` is optional (default the day after `as_of`; dates on or after payday count on payday). `assumptions` are optional what-if overrides for this calculation only; they are not saved.
```json
{
  "item": "Buty", "amount": 400.00, "date": "2026-10-01", "before_payday": true,
  "verdict": "red", "safe_to_spend": -280.97, "left_after": null, "shortfall": 400.00,
  "before": { "projected_end": -1739.27, "status": "danger", "run_out_date": "2026-10-05", "safe_to_spend": -280.97 },
  "after":  { "projected_end": -2139.27, "status": "danger", "run_out_date": "2026-10-01", "safe_to_spend": -680.97 },
  "breakdown": [{ "key": "forecast.balance", "label": "Saldo konta", "amount": 2100.00 }, "…"],
  "goal_delays": [{ "goal_id": "…", "name": "Wakacje", "reach_date_before": "2027-06-30", "reach_date_after": "2027-07-30", "shift_months": 1 }],
  "assumptions": { "payday": 15, "safety_buffer": 300.00, "balance": 2100.00 },
  "explanation": "Nie teraz: …",
  "evidence": { "transaction_ids": ["…"], "figures": ["…"] }
}
```
Verdict: `green` if amount ≤ safe_to_spend, `yellow` if ≤ safe_to_spend + safety_buffer, `red` otherwise. `left_after` = safe − amount when ≥ 0, else `null` and `shortfall` is set. Change assumptions with `PATCH /me`. Errors: `invalid_amount`.

### `POST /simulate/change`
Body `{ "category": "food_delivery", "new_per_week": 50.00 }`.
```json
{
  "category": "food_delivery", "current_per_week": 151.25, "current_per_month": 655.42, "new_per_week": 50.00,
  "monthly_saving": 438.75, "yearly_saving": 5265.00,
  "goals": [{ "goal_id": "…", "name": "Wakacje", "reach_date_before": "2027-06-30", "reach_date_after": "2027-03-30", "shift_months": -3 }],
  "evidence": { "transaction_ids": ["…"], "figures": ["…"] }
}
```

### `GET /alerts`
Computed alerts, dismissed ones excluded, most severe first.
```json
[{
  "id": "run_out:2026-10-05", "type": "run_out", "severity": "critical",
  "title": "Pieniądze mogą skończyć się przed wypłatą", "message": "W tym tempie saldo spadnie poniżej zera 05.10.2026. …",
  "date": "2026-10-05", "amount": 1739.27, "evidence": { "transaction_ids": ["…"], "figures": [] }
}]
```
Types: `run_out` (forecast `danger`), `bnpl` (instalment within 7 days), `duplicate_sub`, `challenge` (a payment broke an active challenge). Severity: `critical`, `warning`, `info`.

### `POST /alerts/{id}/dismiss`
`204`. URL-encode the id (it contains `:` and may contain spaces).

### `GET /home?month=2026-09`
The Home screen for one month (default: the latest). Safe-to-spend and the pay period are always "now". `404 month_not_found` without data.
```json
{
  "month": "2026-09", "as_of": "2026-09-30",
  "income": 5600.00, "expenses": -6052.53, "saved": -452.53, "expenses_change_pct": 11,
  "safe_to_spend": 3189.52,
  "safe_to_spend_evidence": { "transaction_ids": ["…"], "figures": ["…"], "calculation": "6100.00 − 2610.48 − 300.00 = 3189.52" },
  "pay_period": { "start": "2026-09-28", "end": "2026-10-28", "days_left": 28 },
  "status": "ok",
  "by_category": [{ "category": "rent", "amount": -2300.00, "change_pct": 0.0 }],
  "recurring": [{ "merchant": "Spotify", "type": "subscription", "amount": -23.99, "period": "monthly", "next_date": "2026-10-06", "transaction_ids": ["…"] }],
  "narrative": {
    "headline": "Wrzesień 2026: wydatki 6 052,53 zł — o 11% więcej niż miesiąc wcześniej.",
    "bullets": [{ "text": "Dostawy jedzenia: 678,64 zł za 13 zamówień.", "fact_keys": ["cat.food_delivery.total", "cat.food_delivery.count"], "transaction_ids": ["…"] }],
    "ai_generated": true,
    "fact_check": "passed"
  }
}
```
The narrative is built from templates and rephrased by Gemini; every rewritten sentence is fact-checked (`fact_check: "fallback"` means templates).

### `GET /dashboard`
One call for the home screen.
```json
{
  "user": { "name": "Kuba", "persona": "first_job", "language": "pl" },
  "as_of": "2026-09-30",
  "forecast": { "…": "same as GET /forecast" },
  "month": { "month": "2026-09", "income": 5600.00, "expenses": -6052.53, "saved": -452.53, "top_categories": [{ "category": "rent", "amount": 2300.00, "share_pct": 38 }] },
  "opportunities": ["top 3, same items as GET /opportunities (template text)"],
  "total_monthly_saving": 616.05,
  "goals": ["same items as GET /goals"],
  "alerts": ["same items as GET /alerts"],
  "subscriptions": { "monthly_total": 185.98, "count": 4, "duplicates": 2 },
  "bnpl": { "active_plans": 1, "total_remaining": 249.00, "next_date": "2026-10-15", "next_amount": 124.50 }
}
```

## Wrapped

### `GET /wrapped/months`
`[{ "month": "2026-09", "is_new": true }, { "month": "2026-08", "is_new": false }]`, newest first.

### `GET /wrapped?month=2026-09`
`month` defaults to the newest. Unknown format `400 invalid_month`; no data `404 month_not_found`.
```json
{
  "month": "2026-09",
  "total_spent": 6052.53,
  "total_income": 5600.00,
  "change_pct": 11,
  "transaction_count": 64,
  "top_merchant": { "name": "Lidl", "count": 7, "amount": 671.92, "evidence": { "transaction_ids": ["…"], "figures": [] } },
  "top_category": { "category": "rent", "amount": 2300.00, "share_pct": 38 },
  "top_categories": ["top 3"],
  "biggest_change": { "category": "food_delivery", "from": 512.10, "to": 678.64, "change_pct": 33 },
  "cheapest_weekday": "tuesday",
  "potential_savings": 616.05,
  "personality": { "key": "delivery_lover", "emoji": "🛵", "title": "Mistrz Dostaw", "description": "…", "ai_generated": true },
  "captions": { "total_spent": "…", "top_merchant": "…", "biggest_change": "Zakupy spożywcze: 497,17 zł → 1 204,31 zł (+142%)." },
  "cards": [{
    "type": "total_spent", "title": "Wydatki w sumie", "caption": "Wrzesień 2026: wydałeś 6 052,53 zł w 64 transakcjach. 💸",
    "figures": [{ "key": "wrapped.total_spent", "label": "Wydatki", "amount": 6052.53 }],
    "evidence": { "transaction_ids": [], "figures": ["…"] },
    "ai_generated": true
  }]
}
```
Always 8 cards, in this order: `total_spent`, `top_categories`, `top_merchant`, `delivery`, `biggest_day`, `subscriptions`, `month_over_month`, `personality`. `biggest_change` and `top_merchant` may be `null`. Captions are cached per user, month and language.

## Goals
Goal shape (all endpoints):
```json
{
  "id": "…", "name": "Koncert", "emoji": "🎸",
  "target": 1200.00, "saved": 200.00, "remaining": 1000.00, "progress_pct": 16.7,
  "deadline": "2026-12-30", "monthly_plan": 333.34,
  "required_per_week": 76.93, "required_per_month": 333.34,
  "status": "on_track", "short_by": null, "reach_date": "2026-12-30"
}
```
`status`: `on_track` | `behind` | `done`. With a deadline, `behind` means `monthly_plan × months left` < remaining (then `short_by` is set). `reach_date` = as_of + ceil(remaining / monthly_plan) months.

- `GET /goals` → list.
- `POST /goals` body `{ "name", "emoji"?, "target", "saved"?, "deadline"?, "monthly_plan"? }` → `201`. Without `monthly_plan`, the plan is what the deadline requires. Errors: `invalid_name`, `invalid_target`, `invalid_deadline` (must be after `as_of`).
- `PATCH /goals/{id}` (also `PUT`) any subset of the fields plus `clear_deadline: true`.
- `DELETE /goals/{id}` → `204`.
- `POST /goals/{id}/deposit` body `{ "amount": 50.00 }` (negative withdraws) → goal.
- `POST /goals/preview` body `{ "target", "saved"?, "deadline" }` (nothing saved):
```json
{ "required_per_week": 115.39, "required_per_month": 500.00, "monthly_surplus": -1234.50, "verdict": "yellow",
  "plan": [{ "opportunity_id": "bnpl", "title": "Spłać raty i nie bierz nowych", "monthly_saving": 581.99 }],
  "text": "Potrzebujesz 115,39 zł tygodniowo. Uda się, jeśli wprowadzisz poniższe oszczędności." }
```
`green` if the average monthly surplus (income − expenses, last 90 days) covers it, `yellow` if the listed opportunities close the gap, `red` otherwise.
Unknown goal: `404 goal_not_found`.

## Challenges
Challenge shape: `{ "id", "type": "no_delivery"|"no_taxi", "title", "start_date", "end_date", "target", "streak", "best_streak", "status": "active"|"completed"|"failed", "last_check_in_on" }`.

- `GET /challenges` → list.
- `POST /challenges` body `{ "type": "no_delivery", "days": 7 }` (1 to 60) → `201`. Starts today (calendar date).
- `POST /challenges/{id}/checkin` → `{ "challenge": {…}, "broken": false, "break_date": null, "evidence": {…} }`. One per calendar day (`409 already_checked_in`). A matching payment dated on or after the start that was not counted yet resets the streak to 0 (`broken: true`, evidence lists those payments). Reaching `target` completes the challenge.

## Chat
Rate limit: 20 requests per minute per `X-User-Id` on `/chat*` → `429 rate_limited`.

### `POST /chat`
```json
{ "conversation_id": null, "message": "Czy stać mnie na buty za 400 zł?", "language": "pl" }
```
`conversation_id` continues a stored conversation (last 10 messages are sent). Instead of `message` the client may send `messages: [{ role, content }]` (the last `user` item is the question; the rest is history when the conversation is new). `language` defaults to the user's. Errors: `invalid_message` (1 to 1000 chars), `invalid_language`.

JSON response (default):
```json
{
  "conversation_id": "…", "message_id": "…",
  "answer": "Niestety teraz nie jest to dobry moment na ten zakup (werdykt: czerwony). …",
  "tools_used": ["simulate_purchase"],
  "evidence": { "transaction_ids": ["…"], "figures": [{ "key": "purchase.amount", "label": "Zakup", "amount": -400.00 }] },
  "fallback": false,
  "fact_check": "passed",
  "created_at": "2026-10-03T12:00:00Z"
}
```
`fact_check`: `passed` (every number matched a tool result) or `fallback` (model unavailable or failed the check 3 times; `answer` is a template built from the forecast).

Server-sent events (with `Accept: text/event-stream`). The answer is fact-checked as a whole, then streamed in chunks:
```
event: tool       data: {"name":"simulate_purchase"}
event: delta      data: {"text":"Niestety teraz nie jest "}
event: evidence   data: {"transaction_ids":["…"],"figures":[{"key":"…","label":"…","amount":-400.00}]}
event: done       data: {"fact_check":"passed","fallback":false,"conversation_id":"…","message_id":"…","tools_used":["simulate_purchase"]}
event: error      data: {"message":"…"}
```

### `GET /chat/{conversation_id}`
`{ "conversation_id": "…", "messages": [{ "id", "role": "user"|"assistant", "content", "created_at", "tools_used", "evidence", "fallback", "fact_check" }] }` (the last four are `null` for user messages). Unknown: `404 conversation_not_found`.

### `GET /chat/suggestions`
`{ "items": ["Czy wystarczy mi do wypłaty?", "Ile wydałem(-am) na Glovo (wrzesień 2026)?", "…"] }`, built from the user's data.
