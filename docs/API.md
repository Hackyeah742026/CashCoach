# API Contract

Base URL: `http://localhost:5080/api` · JSON · errors use RFC 7807 Problem Details.

**Money format:** amounts are **decimal strings** in PLN (`"-412.30"`). Expenses are negative, income positive. Dates use ISO `YYYY-MM-DD`.

Keep this file in sync with `backend/src/CashCoach.Api/Contracts/` and `frontend/src/types/index.ts`.

---

## Health
`GET /health` returns `200 { "status": "ok", "ai": "ok" | "degraded" }`

## Settings
`GET /settings` · `PUT /settings`
```json
{ "language": "pl", "payday": 10, "safetyBuffer": "300.00", "currentBalance": "1840.55" }
```

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
  "detectedBalance": "1840.55"
}
```

### `GET /transactions?from=&to=&category=&merchant=&needsReview=`
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
  "byCategory": [{ "category": "food_delivery", "amount": "-412.30", "changePct": 34.0 }],
  "recurring": [{ "merchant": "Spotify", "amount": "-23.99", "period": "monthly", "nextDate": "2026-09-05" }],
  "narrative": {
    "headline": "Sierpień: wydałaś/eś 2 875 zł, o 9% więcej niż w lipcu.",
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
  "assumptions": { "payday": 10, "safetyBuffer": "300.00", "currentBalance": "1840.55" }
}
```
`assumptions` is optional and falls back to the settings. `installments`: `{ "count": 3, "monthlyAmount": "400.00" }`.

Response:
```json
{
  "verdict": "yellow",
  "safeToSpend": "940.55",
  "shortfall": "259.45",
  "breakdown": [
    { "label": "Current balance", "amount": "1840.55" },
    { "label": "Upcoming bills before payday (rent share, Spotify, phone)", "amount": "-600.00", "transactionIds": ["…"] },
    { "label": "Safety buffer", "amount": "-300.00" }
  ],
  "assumptions": { "payday": 10, "safetyBuffer": "300.00", "currentBalance": "1840.55" },
  "explanation": { "text": "…", "tips": ["…"], "aiGenerated": true, "factCheck": "passed" }
}
```
Verdict rules (deterministic): `green` if price ≤ safeToSpend · `yellow` if price ≤ safeToSpend + buffer · `red` otherwise.

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

## Categories (enum)
`groceries`, `food_delivery`, `restaurants_cafes`, `transport`, `rent_bills`, `subscriptions`, `shopping`, `health_beauty`, `entertainment`, `education`, `travel`, `transfers_people`, `bnpl`, `cash`, `income_salary`, `income_other`, `savings`, `other`, `uncategorized`
