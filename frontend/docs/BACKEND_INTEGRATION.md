# Backend integration plan

> **Status (2026-10-03): implemented.** With `VITE_USE_MOCKS=false` the app runs on the real API: `src/api/session.ts` (user id), `src/api/backend.ts` + `src/api/adapters.ts` (DTO mapping), `client.ts` (real branch). Backend gaps B1, B2, B4 (first instalment), B5 and B6 are closed (`POST /users`, `POST /import/demo`, `GET /home`, `sum`, `captions`), plus what-if `assumptions` on `/simulate/purchase`. Categories were not renamed: the frontend `Category` type is the union of both sets. Still open: B3 (goal AI tips) and the §7 features (forecast chart, alerts, what-if slider, challenges, deposit, export).

How to switch the frontend from mocks (`VITE_USE_MOCKS=true`) to the real API. The backend contract is `docs/API.md`; the Gemini chat runs entirely on the backend (`POST /chat`), and the frontend never sees a key.

## 1. What differs today

| Topic | Frontend (mocks, `src/types`) | Backend (`docs/API.md`) | Decision |
|---|---|---|---|
| JSON casing | camelCase | snake_case | Map in one adapter layer (`src/api/adapters.ts`). Components keep the current types |
| Money | decimal **string** (`"412.30"`) | JSON **number** (`412.30`) | Adapter converts with `n.toFixed(2)`: formatting only, no math |
| User | none | `X-User-Id` header on every call | New `src/api/session.ts` (user id in `localStorage`) |
| Errors | RFC 7807 (`detail`, `title`) | `{ error: { code, message } }` | Fix `request()` in `client.ts` |
| Categories | 19 values (`restaurants_cafes`, `rent_bills`, `income_salary`…) | 15 values (`restaurants`, `rent`, `utilities`, `salary`…) | Align the frontend to the backend's 15 (see §5) |
| "Today" | `MOCK_TODAY` | `as_of` from `GET /me` (latest transaction date) | `todayIso()` returns the cached `as_of` |
| Settings | `GET/PUT /settings` | `GET/PATCH /me` | Adapter (§3) |
| Home summary | one `Summary` object | `GET /dashboard` + `/summary` + `/subscriptions` | Small backend addition (§4, B2), then one call |

## 2. Session and request plumbing (do first)

1. **`src/api/session.ts`**: `getUserId()`, `setUserId(id)`, `clearUserId()` on `localStorage` key `cashcoach.userId` (wrapped in try/catch), plus a cached `asOf`.
2. **`request()` in `client.ts`**:
   - add `X-User-Id` when a user id exists;
   - parse errors as `body.error.message` / `body.error.code` and keep `code` on `ApiError`;
   - on `401 missing_user_id` or `404 user_not_found`: `clearUserId()` and send the user to `/onboarding`.
3. **`.env`**: `VITE_USE_MOCKS=false`, `VITE_API_URL=http://localhost:5080/api`. CORS already allows `http://localhost:5173`.
4. **Route guard** (`RequireOnboarded`): with no stored user id, treat it as `onboarded: false` without calling the API.

## 3. Endpoint mapping (`client.ts` + `adapters.ts`)

Keep every exported function name and return type in `client.ts`; only the non-mock branch changes.

| Frontend function | Backend call | Mapping notes |
|---|---|---|
| `getSettings()` | `GET /me` | `language`, `payday`, `safetyBuffer = safety_buffer`, `currentBalance = balance`, `onboarded = has_data`, `availableMonths = available_months`. Cache `as_of` in the session |
| `updateSettings(u)` | `PATCH /me` | `{ language, payday, safety_buffer: +u.safetyBuffer, balance: +u.currentBalance }` |
| `deleteAllData()` | `DELETE /me` | then `clearUserId()` |
| `importDemoData()` | `POST /demo/login { persona }` | `setUserId(user_id)`. Demo login returns the profile, not import counts: build the result screen from `GET /transactions?limit=1` (`total` → `imported`) and `available_months` (→ `from`/`to`), and hide the rules/AI split. Add a persona picker (Student, Pierwsza praca, Raty BNPL) on onboarding; default `bnpl_heavy` tells the strongest demo story |
| `importTransactions(file)` | `POST /import` (multipart `file`) | Needs a user first: see backend gap B1. Map `imported`, `duplicatesSkipped = skipped_duplicates`, `categorizedByRules = categorized.dictionary + categorized.fuzzy`, `categorizedByAi = categorized.llm`, `needsReview = categorized.other`, `from/to = period`, `detectedBalance = null` |
| `getTransactionsByIds(ids)` | `GET /transactions?ids=a,b,c&limit=500` | `amount` → string, `description = raw_description`, `isRecurring = is_recurring`, `categorySource: 'rule'`, `confidence: 1`, `count = total`. `total` (money sum): see B5 |
| `getSummary(month)` | after B2: `GET /dashboard?month=` | `safeToSpend = forecast.safe_to_spend`, `safeToSpendEvidence = forecast.evidence`, `payPeriod = { start, end: forecast.next_payday, daysLeft: forecast.days_left }`, `income/expenses/saved = month.*`, `byCategory`, `recurring`, `narrative` |
| `getSavings()` | `GET /opportunities` | `suggestions[] = { id, title, rationale, monthlyImpact: monthly_saving, difficulty, evidence }`, `totalPotential = total_monthly_saving`. Show the ✨ badge only when `ai_generated` |
| `dismissSaving(id)` | `POST /opportunities/{encodeURIComponent(id)}/dismiss` | ids contain `:` |
| `checkAffordability(req)` | `POST /simulate/purchase { amount: +price, date, item }` | If the user edited assumptions, `PATCH /me` first (the backend reads them from the profile). Map `verdict`, `safeToSpend`, `leftAfter = left_after`, `shortfall`, `breakdown = breakdown[{ label, amount }]`, `assumptions = { payday, safetyBuffer, currentBalance: balance }`, `explanation = { text: explanation, tips: [], aiGenerated: false, factCheck: 'passed' }`. Instalments: B4 |
| `getWrappedMonths()` | `GET /wrapped/months` | `isNew = is_new` |
| `getWrapped(month)` | `GET /wrapped?month=` | Fields map 1:1 after camelCase. `captions.totalSpent = cards[total_spent].caption`, `captions.topMerchant = cards[top_merchant].caption`, `captions.biggestChange = cards[month_over_month].caption` (or B6). `personality` adds `key` |
| `getGoals()` | `GET /goals` | money → string; `requiredPerWeek ?? '0'`; `deadline` may be `null` (make the type `IsoDate \| null` and hide "by …"); `aiTip: null` (B3) |
| `createGoal(i)` | `POST /goals { name, emoji, target: +i.target, saved: +i.saved, deadline }` | returns the computed goal |
| `updateGoal(id, i)` | `PUT /goals/{id}` | `tipAccepted` is ignored until B3 |
| `deleteGoal(id)` | `DELETE /goals/{id}` | |
| `previewGoal(req)` | `POST /goals/preview` | `plan[] = { savingId: opportunity_id, title, monthlyImpact: monthly_saving }`. `aiText`: frontend template from `verdict` until B3 |
| `streamChat(messages, lang)` | `POST /chat` with `Accept: text/event-stream` | Body `{ messages, language }` already matches. See §6 |

Run every response through the adapter; keep the adapters pure and unit-testable (`snake → camel`, `number → string` for money fields only).

## 4. Small backend gaps to close

Ordered by impact on the demo. Each is a few lines in the existing services.

| # | Gap | Proposal |
|---|---|---|
| B1 | Real CSV upload needs a user, but only demo login creates one | `POST /users { name?, language }` → profile (new non-demo user). Onboarding: create user → `POST /import` |
| B2 | Home `Summary` has no single source: per-month `by_category` with change %, `recurring` list, pay-period start and the AI `narrative` | Add `?month=` to `GET /dashboard` and include `by_category` (from `SpendingSummaryCalculator`), `recurring` (subscriptions + rent with next dates and transaction ids), `pay_period.start` and `narrative { headline, bullets[{ text, fact_keys, transaction_ids }], ai_generated, fact_check }` built from templates and rewritten by `AiCopywriter` |
| B3 | Goals `aiTip` and preview `aiText` | Template tip from the top opportunity ("Gotuj 2× w tygodniu…"), optionally rewritten by `AiCopywriter`; `tip_accepted` flag on the goal |
| B4 | Affordability instalments (`installments: { count, monthlyAmount }`) | `Simulator.SimulatePurchase` with the first instalment as the amount plus the monthly cost in the breakdown |
| B5 | Evidence drawer shows a money total | Add `sum` to `GET /transactions` when `ids` is set |
| B6 | Wrapped story has a "biggest change" slide but no caption for it | Add a `biggest_change` caption key next to the 8 cards |

Until B2 lands, `getSummary` can be composed in the adapter from `GET /dashboard` (hero, stats, top categories), `GET /subscriptions` (recurring list) and a template narrative from `alerts[0]` and `opportunities[0]`. That keeps the Home screen working on day one.

## 5. Categories

Switch `Category` in `src/types/index.ts` and `src/lib/categories.ts` (labels, colors, icons) to the backend's 15 values: `groceries`, `food_delivery`, `restaurants`, `transport`, `subscriptions`, `shopping`, `entertainment`, `rent`, `utilities`, `health`, `education`, `bnpl`, `transfers`, `salary`, `other`. Old → new: `restaurants_cafes → restaurants`, `rent_bills → rent` (+ `utilities`), `health_beauty → health`, `transfers_people → transfers`, `income_salary → salary`, `income_other`/`cash`/`savings`/`uncategorized → other`. Update the mocks in the same commit so mock mode keeps working.

## 6. Gemini chat

The backend already does the Gemini work: tool calling over the user's data, fact-checking every number, retries, and a template fallback.

1. **Request:** add `X-User-Id`. Keep `{ messages, language }`; after the first answer send `conversation_id` too (from the `done` event) so the server keeps history.
2. **`lib/sse.ts`:** read snake_case payloads:
   - `evidence`: `transactionIds = data.transaction_ids`, `figures = data.figures.map(f => ({ label: f.label, amount: f.amount.toFixed(2) }))`;
   - `done`: `factCheck = data.fact_check`, plus `conversationId = data.conversation_id`, `fallback = data.fallback`, `toolsUsed = data.tools_used`;
   - `tool` (`{ name }`) and `delta` (`{ text }`) are unchanged.
3. **`useChat`:** store `conversationId` and reset it in `reset()`. Show a subtle "template answer" note when `fallback` is true. `toolsUsed` can render as chips ("📊 forecast_until_payday" → i18n labels).
4. **Starter questions:** load `GET /chat/suggestions` for the empty chat state.
5. **Errors:** `429 rate_limited` → friendly "slow down" message; `event: error` → existing error bubble.
6. **Latency:** the answer streams only after the fact check, so the first `delta` arrives after 3 to 6 s. Show the `tool` events right away ("Sprawdzam prognozę…") so the wait feels alive.
7. **Free-tier quota:** when Gemini is rate-limited the backend answers with the template (`fallback: true`). That's expected; the demo keeps working.

## 7. New backend features worth surfacing

These exist in the API but have no UI yet, in rough order of demo value:

1. **Forecast chart and run-out warning** (`GET /forecast` → `series`, `run_out_date`, `status`): a line on Home with the zero line and payday marker. Strongest story for the `bnpl_heavy` persona.
2. **Alerts** (`GET /alerts`, dismiss): a stack of cards above the hero (critical first).
3. **"What if" slider** (`POST /simulate/change`): "Delivery per week: 150 → 50 zł" shows monthly and yearly savings and goal dates moving.
4. **Challenges** (`GET/POST /challenges`, `/checkin`): "7 dni bez dostaw" streak card on Goals.
5. **Goal deposit** (`POST /goals/{id}/deposit`): a "+ Wpłać" button on `GoalCard`.
6. **Privacy** (`GET /me/export`, `DELETE /me`): buttons in Settings next to "Delete all data".

## 8. Order of work

1. Session + `request()` + error format + env (§2). Check `GET /me` in the browser.
2. Categories (§5) and the adapter module with tests for money/casing.
3. Onboarding demo login with persona picker → Settings → route guard.
4. Opportunities, Wrapped, Goals (direct mappings).
5. Chat SSE (§6).
6. Home: adapter-composed summary first, then B2 on the backend.
7. Backend gaps B1, B3 to B6.
8. New features (§7) as time allows.

## 9. Done when

- With `VITE_USE_MOCKS=false` and the API running, the demo flow works end to end for all three personas: onboarding → Home → Wrapped → Goals (create, preview) → "Can I afford it?" → Chat.
- Every number on screen comes from the API; the frontend does no arithmetic on money.
- Every AI sentence with a number opens the Evidence drawer with real transactions (`GET /transactions?ids=`).
- Mock mode still works (`VITE_USE_MOCKS=true`).
- `npm run build && npm run lint` pass; `cd backend && dotnet test` passes.
