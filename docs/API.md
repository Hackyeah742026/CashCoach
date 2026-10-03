# API Contract

Base URL: `http://localhost:5080/api` · JSON with `snake_case` property names and enum values.

**Errors:** `{ "error": { "code": "…", "message": "…" } }` with a matching HTTP status. Unexpected failures return `500` with code `internal_error`.

**User:** endpoints below `GET /health` and `POST /demo/login` require the `X-User-Id` header (user GUID). Missing header: `401 missing_user_id`. Not a GUID: `400 invalid_user_id`. Unknown user: `404 user_not_found`.

**Money:** amounts are JSON **numbers** in złoty with 2 decimals (`-11.00`). Stored as integer grosze. Expenses are negative, income positive. Dates are ISO `YYYY-MM-DD`.

Forecast, savings opportunities, wrapped, goals and chat are not part of this contract yet.

## Health
`GET /health` → `200 { "status": "ok" }`. No `X-User-Id`.

## Demo login
`POST /demo/login` · no `X-User-Id`. Body `{ "persona": "student" | "first_job" | "bnpl_heavy" }`.

Creates or reuses the demo user (Ola, Kuba, Maja) and imports that persona's synthetic CSV once.
```json
{ "user_id": "…", "name": "Ola", "persona": "student", "language": "pl", "has_consent": false, "has_data": true }
```

## Profile
`GET /me` returns the same shape as demo login.

`PATCH /me` body `{ "name": "Aleksandra", "language": "en" }`. Either field may be omitted. Language is `pl` or `en`.

`POST /me/consent` body `{ "accepted": true, "scopes": ["transactions", "ai_coach"] }`. `accepted: false` is `400`.
```json
{ "consent_at": "2026-10-03T12:00:00+00:00", "scopes": ["transactions", "ai_coach"] }
```

## Import
`POST /import` multipart field `file`. CSV header `date;amount;description;currency`, `;` delimiter, decimal comma or dot. Duplicate rows (same user, date, amount and raw description) are skipped.
```json
{
  "imported": 214,
  "skipped_duplicates": 3,
  "categorized": { "dictionary": 171, "fuzzy": 22, "llm": 18, "other": 3 },
  "recurring_found": { "subscriptions": 5, "bnpl": 2, "salary_day": 28 },
  "period": { "from": "2026-07-01", "to": "2026-09-30" }
}
```

## Transactions
`GET /transactions?from=&to=&category=&q=&limit=50&offset=0`

`total` is the filtered row count, not a money sum. `q` matches merchant or raw description.
```json
{
  "total": 12,
  "items": [
    {
      "id": "…",
      "date": "2026-08-14",
      "amount": -38.90,
      "merchant": "Glovo",
      "category": "food_delivery",
      "raw_description": "GLOVO*ORDER 123 KRAKOW",
      "channel": "card",
      "is_recurring": false,
      "is_bnpl": false
    }
  ]
}
```

`PATCH /transactions/{id}` body `{ "category": "restaurants", "apply_to_merchant": true }`.
```json
{ "id": "…", "category": "restaurants", "updated_count": 6 }
```
Unknown id: `404 transaction_not_found`. Unknown category: `400 invalid_category`.

## Summary
`GET /summary?period=this_month|last_month|last_3_months`

Periods are calendar months relative to the user's latest transaction. `total_spent` is the positive magnitude of expenses. `share` is a fraction of that total. `vs_prev_pct` is the percent change against the previous period of the same length, or `null` when nothing was spent then.
```json
{
  "period": { "from": "2026-09-01", "to": "2026-09-30" },
  "total_spent": 2875.40,
  "categories": [
    {
      "category": "food_delivery",
      "amount": 412.30,
      "count": 14,
      "share": 0.1434,
      "vs_prev_pct": 34.0,
      "top_merchants": [{ "merchant": "Glovo", "amount": 280.00, "count": 9 }]
    }
  ]
}
```

## Subscriptions
`GET /subscriptions`
```json
{
  "monthly_total": 84.97,
  "items": [
    {
      "id": "…",
      "merchant": "Spotify",
      "amount": 23.99,
      "next_date": "2026-10-06",
      "group": "music",
      "duplicate": true,
      "user_confirmed": null
    }
  ]
}
```
`group` is `music`, `video` or `null`. `duplicate` is true when another active subscription is in the same group. `user_confirmed` stays `null` until the user answers.

`PATCH /subscriptions/{id}` body `{ "still_using": false }`.
```json
{ "id": "…", "user_confirmed": false, "new_opportunity_id": null }
```
`new_opportunity_id` stays `null` until savings opportunities exist. Unknown id: `404 subscription_not_found`.

## BNPL
`GET /bnpl`. Amounts are positive. `explainer` is a fixed template, not a model.
```json
{
  "active_plans": 2,
  "total_remaining": 349.98,
  "items": [
    {
      "provider": "Klarna",
      "merchant": "Zalando",
      "instalment": 99.99,
      "paid": 2,
      "total": 4,
      "remaining": 199.98,
      "next_date": "2026-10-15"
    }
  ],
  "explainer": "Masz 2 aktywne plany…"
}
```

## Categories
`groceries`, `food_delivery`, `restaurants`, `transport`, `subscriptions`, `shopping`, `entertainment`, `rent`, `utilities`, `health`, `education`, `bnpl`, `transfers`, `salary`, `other`.
