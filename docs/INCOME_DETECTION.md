# Income detection and confirmation

> **Status (2026-10-03): implemented** (backend, frontend, tests, browser-checked). One addition to the plan: when the balance estimate is 0 zł, the Home card asks for the balance directly instead of "about 0 zł?".

Goal: onboarding stops asking for the payday and balance. The system finds the user's regular income in the imported CSV (day of month and amount), shows its guess, and the user confirms it ("Yes, that's right") or corrects it.

## Decisions
- **Question:** "Do you get a regular income (salary, stipend…)?" covers students, whose main income is often a stipend.
- **Balance:** read from the CSV when it has a balance column (real mBank and PKO exports have "saldo po operacji"). Otherwise the backend's estimate is used, marked `balance_is_estimate`, and Home shows a dismissible "Is your balance about X? [Yes] [Correct it]" card. The safety buffer keeps its 300 zł default and stays in Settings.

## User flow

```
1. Welcome (language)
2. Upload CSV  or  "Try with demo data" (persona picker)
3. Income
   "Do you get a regular income (salary, stipend…)?"   [Yes]  [No]
     Yes → guess from the CSV:
           "It looks like you get 5 600 zł around the 28th of each month (Wynagrodzenie)."
           [See the transactions] (Evidence drawer)   [Yes, that's right]  [Correct it]
             Correct it → day of month (1–31) or "last working day" + amount
           No guess → the same form, empty
     No  → no payday; forecasts run to the end of the month
4. Import result → Home
```

## Backend

### Domain (`User`)
| Field | Type | Notes |
|---|---|---|
| `Payday` | int? | existing; the day number (31 for "last working day") |
| `PaydayRule` | `fixed_day` · `last_working_day` | new |
| `SalaryGr` | long? | new; confirmed monthly income |
| `IncomeStatus` | `unknown` · `confirmed` · `none` | new; `unknown` until the user answers |
| `IncomeSource` | text? | new; e.g. `Wynagrodzenie` |

`AppDbContext.SchemaVersion` goes to 3 (old demo databases are recreated).

### `IncomeDetector` (Core, pure)
Input: transactions. Output: ranked `IncomeCandidate { source, kind (salary · stipend · other), day, day_rule, amount (median), amount_min, amount_max, months_seen, confidence (high · medium · low), transaction_ids }`.

1. Incoming payments, grouped by a title keyword (`WYNAGRODZENIE`, `PENSJA`, `WYPLATA`, `STYPENDIUM`) or else by merchant.
2. One payment per calendar month (the largest); at least 2 months.
3. Day: for every day 1–31, a payment "matches" if it falls on that day or on the Friday before it when the day is a weekend; it is "near" within ±3 days. Pick the day with most matches (ties: most near, then later day). Every payment must be near.
4. "Last working day": all payments on the last weekday of their month with different day numbers → `last_working_day`.
5. Amount: median; every month within ±25% of it (one outlier allowed from 3 months up, e.g. a bonus).
6. Confidence: high = 3+ months, all matching, amounts within 5%; medium = 3+ months or 2 matching within 5%; low otherwise.
7. Ranked by amount, largest first (the main income).

### API
- `GET /income/detection` → `{ guess, others, confirmed }`.
- `PUT /me/income` → `{ has_income: true, day: 28, day_rule: "fixed_day", amount: 5600.00, source? }` or `{ has_income: false }` → profile.
- `GET /me` gains `income: { status, day, day_rule, amount, source }`.
- `POST /import` response gains `detected_balance` (zł or `null`).

### Rules
- An import refreshes the payday guess only while `IncomeStatus` is `unknown`; a confirmed payday or "no income" is never overwritten (this includes `POST /import/demo`).
- `none` → payday `null` → the forecast horizon is the 1st of next month.
- `last_working_day` → the next payday is the last Monday-to-Friday of the month (public holidays are not modelled).
- CSV: optional `balance` column. The balance after the newest row becomes the user's balance.
- The chat's `get_balance` tool also returns the confirmed income.

## Frontend
- `Onboarding.tsx`: remove `FinancesStep`; the steps become Welcome → Upload → Income → Result.
- New `components/onboarding/IncomeStep.tsx` (question → guess card → form) and `IncomeForm.tsx`, which Settings reuses.
- `Settings`: an "Income" card (shows the confirmed income, edit with `IncomeForm`) plus balance and buffer; the balance shows "≈ estimate" while unconfirmed.
- Home: `BalanceCheckCard` while `balanceIsEstimate`.
- API layer: `getIncomeDetection()`, `confirmIncome()`, adapters, `useIncomeDetection` / `useConfirmIncome`, and mock handlers.
- PL/EN strings for every new text.

## Tests
- Core `IncomeDetector`: fixed day, weekend shift, bonus month, last working day, two incomes, irregular (no guess), one month only.
- API: each persona's guess (student: stipend 1 650 zł on the 10th; first job: 5 600 zł on the 28th; BNPL: 4 300 zł on the 15th); confirm and correct; a later import keeps a confirmed payday; "no income" → forecast to the end of the month; CSV balance column.
- Browser run: demo data → "Yes, that's right" → Home shows the confirmed payday.
