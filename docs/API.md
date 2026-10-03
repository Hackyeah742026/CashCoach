# API Contract

Base URL: `http://localhost:5080/api` · JSON · errors use RFC 7807 Problem Details.

**Money format:** amounts are **decimal strings** in PLN (`"-412.30"`). Expenses are negative, income positive. Dates use ISO `YYYY-MM-DD`.

Keep this file in sync with `backend/src/CashCoach.Api/Contracts/` and `frontend/src/types/index.ts`.

---

## Health
`GET /health` returns `200 { "status": "ok", "ai": "ok" | "degraded" }`

## Settings
`GET /settings`
```json
{
  "language": "pl",
  "payday": 10,
  "safetyBuffer": "300.00",
  "currentBalance": "1601.52",
  "onboarded": true,
  "availableMonths": ["2026-06", "2026-07", "2026-08"]
}
```
`PUT /settings` takes any subset of `language`, `payday`, `safetyBuffer`, `currentBalance` and returns the full settings. `onboarded` and `availableMonths` are read-only; they become set after an import. The frontend's route guard sends users with `onboarded: false` to onboarding.

## Data
`DELETE /data` deletes all transactions, goals, rules and settings. Returns `204`.

## Transactions

### `POST /transactions/import`
`multipart/form-data`, field `file` (CSV). Optional `bank` (`mbank` | `pko`); it's auto-detected when omitted.
```json
{
  "importId": "b1c2…",
  "bank": "mbank",
  "imported": 312,
  "duplicatesSkipped": 0,
  "categorizedByRules": 271,
  "categorizedByAi": 37,
  "needsReview": 4,
  "from": "2026-06-01",
  "to": "2026-08-31",
  "detectedBalance": "1601.52"
}
```

### `POST /transactions/import/demo`
No body. Loads the synthetic demo data set (same response as `/transactions/import`, with `"bank": "demo"`). Used by "Try it with demo data" on onboarding.

### `GET /transactions?from=&to=&category=&merchant=&needsReview=&ids=`
`ids` is a comma-separated list of transaction IDs; the Evidence drawer uses it to load the transactions behind a claim.
```json
{
  "items": [
    {
      "id": "t_0192",
      "date": "2026-08-14",
      "amount": "-38.90",
      "merchant": "Glovo",
      "description": "GLOVO*ZAMOWIENIE KRAKOW",
      "category": "food_delivery",
      "categorySource": "rule",
      "confidence": 1.0,
      "isRecurring": false
    }
  ],
  "total": "-412.30",
  "count": 14
}
```

### `PATCH /transactions/{id}/category`
```json
{ "category": "groceries", "applyToMerchant": true }
```
If `applyToMerchant` is true, a user rule is created and all transactions from that merchant are re-categorized. Returns the updated count.

## Insights

### `GET /insights/summary?month=2026-08`
```json
{
  "month": "2026-08",
  "income": "3200.00",
  "expenses": "-2875.40",
  "saved": "324.60",
  "safeToSpend": "940.55",
  "safeToSpendEvidence": {
    "transactionIds": ["t_s05", "…"],
    "figures": [
      { "label": "Saldo konta", "amount": "1601.52" },
      { "label": "Rachunki przed wypłatą", "amount": "-360.97" },
      { "label": "Poduszka bezpieczeństwa", "amount": "-300.00" }
    ],
    "calculation": "1601.52 − 360.97 − 300.00"
  },
  "payPeriod": { "start": "2026-08-10", "end": "2026-09-10", "daysLeft": 12 },
  "expensesChangePct": 9,
  "byCategory": [{ "category": "food_delivery", "amount": "-412.30", "changePct": 34.0 }],
  "recurring": [{ "merchant": "Spotify", "amount": "-23.99", "period": "monthly", "nextDate": "2026-09-05", "transactionIds": ["t_s01"] }],
  "narrative": {
    "headline": "Sierpień: wydatki 2 875,40 zł — o 9% więcej niż w lipcu.",
    "bullets": [
      { "text": "Dostawy jedzenia: 412,30 zł (14 zamówień).", "factKeys": ["cat.food_delivery.month"], "transactionIds": ["t_0192", "…"] }
    ],
    "aiGenerated": true,
    "factCheck": "passed"
  }
}
```

### `GET /insights/savings?month=2026-08`
```json
{
  "suggestions": [
    {
      "id": "s_delivery",
      "title": "Gotuj 2× w tygodniu zamiast Glovo",
      "rationale": "…",
      "monthlyImpact": "160.00",
      "difficulty": "medium",
      "evidence": { "transactionIds": ["t_0192", "…"], "calculation": "14 orders × 29.45 avg − 8 × groceries 9.50" }
    }
  ],
  "totalPotential": "310.00"
}
```

### `POST /insights/savings/{id}/dismiss`
Returns `204`. The suggestion is hidden from future lists.

## Affordability

### `POST /affordability`
```json
{
  "item": "Bilety na koncert + pociąg do Gdańska",
  "price": "1200.00",
  "date": "2026-09-20",
  "installments": null,
  "assumptions": { "payday": 10, "safetyBuffer": "300.00", "currentBalance": "1601.52" }
}
```
`assumptions` is optional and falls back to the settings. `installments`: `{ "count": 3, "monthlyAmount": "400.00" }`.

Response:
```json
{
  "verdict": "yellow",
  "safeToSpend": "940.55",
  "leftAfter": null,
  "shortfall": "259.45",
  "breakdown": [
    { "label": "Current balance", "amount": "1601.52" },
    { "label": "Upcoming bills before payday (CityFit, Play, Netflix…)", "amount": "-360.97", "transactionIds": ["…"] },
    { "label": "Safety buffer", "amount": "-300.00" }
  ],
  "assumptions": { "payday": 10, "safetyBuffer": "300.00", "currentBalance": "1601.52" },
  "explanation": { "text": "…", "tips": ["…"], "aiGenerated": true, "factCheck": "passed" }
}
```
Verdict rules (deterministic): `green` if price ≤ safeToSpend · `yellow` if price ≤ safeToSpend + buffer · `red` otherwise. `leftAfter` = safeToSpend − price when that's ≥ 0, otherwise `null` (then `shortfall` is set). The frontend never subtracts amounts itself.

## Chat

### `POST /chat` (SSE stream)
Request:
```json
{ "messages": [{ "role": "user", "content": "Ile wydałem na Bolta w sierpniu?" }], "language": "pl" }
```
Server-sent events:
```
event: delta      data: {"text":"W sierpniu "}
event: tool       data: {"name":"get_transactions","input":{…}}
event: evidence   data: {"transactionIds":["…"],"figures":[{"label":"Bolt, 08.2026","amount":"-186.00"}]}
event: done       data: {"factCheck":"passed"}
event: error      data: {"message":"…"}
```

## Wrapped

### `GET /wrapped/months`
```json
[{ "month": "2026-08", "isNew": true }, { "month": "2026-07", "isNew": false }]
```
Newest first.

### `GET /wrapped?month=2026-08`
```json
{
  "month": "2026-08",
  "totalSpent": "2875.40",
  "changePct": 9,
  "transactionCount": 55,
  "topMerchant": { "name": "Glovo", "count": 14, "amount": "412.30", "evidence": { "transactionIds": ["t_g01", "…"] } },
  "topCategory": { "category": "rent_bills", "amount": "1135.00", "sharePct": 39 },
  "biggestChange": { "category": "restaurants_cafes", "from": "98.00", "to": "161.00", "changePct": 64 },
  "cheapestWeekday": "tuesday",
  "potentialSavings": "308.99",
  "personality": { "emoji": "🌙", "title": "Weekendowy Smakosz", "description": "…", "aiGenerated": true },
  "captions": { "totalSpent": "…", "topMerchant": "…", "biggestChange": "…" }
}
```
All numbers are computed. `personality` and `captions` are AI-written (fact-checked like other AI text). `biggestChange` may be `null`.

## Goals

### `GET /goals`
```json
[
  {
    "id": "g1",
    "name": "Koncert w Gdańsku",
    "emoji": "🎸",
    "target": "1200.00",
    "saved": "540.00",
    "deadline": "2026-09-20",
    "requiredPerWeek": "210.00",
    "status": "behind",
    "shortBy": "189.00",
    "aiTip": { "text": "…", "aiGenerated": true, "accepted": false }
  }
]
```
`status`: `on_track` | `behind` | `done`. `shortBy` is set only when `behind`. `aiTip` may be `null`.

### `POST /goals` · `PUT /goals/{id}` · `DELETE /goals/{id}`
Body for POST/PUT: `{ "name", "emoji", "target", "saved", "deadline" }`. PUT also accepts `{ "tipAccepted": true }`. Both return the computed goal (shape above). DELETE returns `204`.

### `POST /goals/preview`
Live calculation while the user fills in the goal form; nothing is saved.
```json
{ "target": "1200.00", "saved": "0.00", "deadline": "2026-09-20" }
```
Response:
```json
{
  "requiredPerWeek": "357.14",
  "verdict": "red",
  "plan": [{ "savingId": "s_delivery", "title": "Gotuj 2× w tygodniu zamiast Glovo", "monthlyImpact": "160.00" }],
  "aiText": "…"
}
```

## Categories (enum)
`groceries`, `food_delivery`, `restaurants_cafes`, `transport`, `rent_bills`, `subscriptions`, `shopping`, `health_beauty`, `entertainment`, `education`, `travel`, `transfers_people`, `bnpl`, `cash`, `income_salary`, `income_other`, `savings`, `other`, `uncategorized`
