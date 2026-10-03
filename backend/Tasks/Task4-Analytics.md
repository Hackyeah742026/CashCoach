# Task 4: Analytics (PDF B10, B13 to B17, B23, B24)

Goal: forecast, savings, simulations, Wrapped, goals, challenges, alerts and the dashboard. All numbers are computed by deterministic C#.

## Steps
1. `ForecastService` (Core)
   - `days_left`, `fixed_upcoming`, `daily_variable` (median over last 30 days), `projected_end`.
   - `run_out_date` by day-by-day simulation; `status` ok (>= 200 zl) / tight (0-200) / danger (< 0).
   - Daily balance series for the chart.
2. `OpportunitiesService` (Core)
   - Rules: food delivery, duplicate subscription, unused subscription, small daily buys, taxi rides, BNPL.
   - Each returns `{id, type, title_key, monthly_saving, yearly_saving, evidence}`; sort and return top 5.
3. `SimulationService` (Core)
   - `SimulatePurchase(amount, date)` returns before/after, new status, goal delays.
   - `SimulateChange(category, new_per_week)` returns monthly saving and new goal date.
   - Goal reach date = today + ceil((target - saved) / monthly_plan) months.
4. `WrappedService` (Core)
   - Monthly stats: totals, top 3 categories, top merchant, delivery total, biggest day, subscriptions, month-over-month, fun equivalent, personality label.
   - 8 card types with template captions.
5. Endpoints
   - `GET /forecast`, `GET /opportunities`, `GET /wrapped?month=`
   - `GET/POST /goals`, `PATCH/DELETE /goals/{id}`, `POST /goals/{id}/deposit`
   - `POST /simulate/purchase`, `POST /simulate/change`
   - `GET/POST /challenges`, `POST /challenges/{id}/checkin` (delivery transaction breaks the streak)
   - `GET /alerts`, `POST /alerts/{id}/dismiss` (run_out, bnpl, duplicate_sub, challenge)
   - `GET /dashboard` composing the above
   - `DELETE /me`, `GET /me/export`

## Done when
- The BNPL persona shows `danger` with a run-out date.
- Each persona gets 3+ opportunities with amounts.
- Wrapped returns 8 cards per persona.
- `/dashboard` matches the PDF JSON shape.
- Unit tests cover forecast, opportunities and simulations.
