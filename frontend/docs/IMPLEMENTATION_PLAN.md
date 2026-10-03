# Frontend Implementation Plan

Builds the screens from [SCREENS.md](SCREENS.md) using the tokens and layout classes from `src/index.css` ([DESIGN.md](DESIGN.md)).

**Strategy: mock-first.** Build every screen against local mock JSON, then switch to the real API with one env flag. The frontend never waits for the backend.

> **Status (2026-10-03):** Phases 0–5 are implemented and run on mock data. Mocks are typed TS modules in `src/api/mocks/` (`data.ts` = demo persona, `handlers.ts` = fake backend) rather than JSON files. Not built: the `/dev/ui` component gallery (Phase 3) and the review queue for low-confidence categories. Remaining: **Phase 6** (switch to the real API) once the backend implements the endpoints in root `docs/API.md`.

---

## Phase 0: Setup (≈ 30 min)

### Install
```bash
cd frontend
npm i react-router @tanstack/react-query lucide-react clsx
npm i html-to-image          # Wrapped "Share" → PNG (P2, can install later)
```
| Package | Why |
|---|---|
| `react-router` | Tabs and pages, `NavLink` gives `aria-current="page"` for free (the nav CSS uses it) |
| `@tanstack/react-query` | Fetching, caching, loading and error states |
| `lucide-react` | Icons (tab bar, buttons) |
| `clsx` | Conditional class names |

Add each one to `docs/AI_DISCLOSURE.md` (root).

### Config
- `vite.config.ts`: add `envDir: '..'` (the `.env` lives at the repo root).
- Root `.env`: `VITE_API_URL=http://localhost:5080/api` and `VITE_USE_MOCKS=true`.
- Delete the contents of `src/App.css` (Vite demo leftovers), or delete the file and its import.

### Target folder structure
```
src/
  main.tsx                 providers: QueryClientProvider, BrowserRouter
  App.tsx                  <Routes>
  layout/
    AppShell.tsx           app-bar + app-nav + <Outlet/>
  pages/
    Home.tsx  Wrapped.tsx  Chat.tsx  Goals.tsx  Onboarding.tsx  Settings.tsx
  components/
    ui/                    shared: MoneyText, StatTile, AiText, Sheet, EvidenceButton,
                           EvidenceDrawer, VerdictCard, ProgressRing, ProgressBar, Chip,
                           Skeleton, EmptyState
    home/                  SafeToSpendHero, InsightCard, QuickAfford, AffordSheet,
                           CategoryBreakdown, SpendingChart, RecurringList, SavingsCard
    wrapped/               WrappedMonthList, StoryPlayer, StoryCard, cards/*.tsx
    chat/                  ChatMessages, MessageBubble, SuggestionChips, ChatInput
    goals/                 GoalCard, GoalSheet, GoalPlan
    onboarding/            TransactionUpload, StepDots
  api/
    client.ts              typed fetch functions (real + mock switch)
    mocks/                 *.json fixtures + mockChatStream.ts
  hooks/                   useSummary, useSavings, useAffordability, useWrapped,
                           useGoals, useChat, useSettings
  i18n/
    strings.ts             { pl: {...}, en: {...} } + useT() hook
  lib/
    format.ts              formatMoney, formatDate, formatPercent
  types/
    index.ts               DTO types (mirror root docs/API.md)
```
Map the existing empty files: `Dashboard.tsx` → `Home.tsx`, `Afford.tsx` → delete (it becomes `AffordSheet`), and move `ChatPanel`, `SavingsCard` and the others into the subfolders above.

**Done when:** `npm run dev` runs and `npm run build` passes.

---

## Phase 1: Foundations (≈ 1.5 h)

1. **Types** in `types/index.ts`, mirroring root `docs/API.md`:
   `Money = string` (decimal string, never `number`), `Category`, `Transaction`, `Evidence`, `AiNarrative { text/bullets, aiGenerated, factCheck }`, `Summary`, `SavingSuggestion`, `AffordabilityRequest/Response`, `Verdict = 'green'|'yellow'|'red'`, `ChatMessage`, `Settings`, plus the new `Wrapped` and `Goal` (see "Backend contract additions" below).
2. **`lib/format.ts`**
   ```ts
   const pln = new Intl.NumberFormat('pl-PL', { style: 'currency', currency: 'PLN' })
   export const formatMoney = (v: string) => pln.format(Number(v))  // display only, never do math
   ```
3. **`i18n/strings.ts`**: a plain object `{ pl, en }` and a `useT()` hook that reads the language from settings, falling back to `pl`. No i18n library.
4. **`api/client.ts`**: one `request<T>(path, init)` helper, then one function per endpoint. Mock switch:
   ```ts
   const USE_MOCKS = import.meta.env.VITE_USE_MOCKS === 'true'
   export const getSummary = (month: string) =>
     USE_MOCKS ? mock(summaryJson) : request<Summary>(`/insights/summary?month=${month}`)
   // mock() = Promise that resolves after ~400 ms, so you can see loading states
   ```
5. **Mocks** in `api/mocks/`: `summary.json`, `savings.json`, `affordability.json`, `wrapped.json`, `goals.json`, `transactions.json`. Use the demo persona numbers from root `docs/DEMO_SCRIPT.md` so the UI already tells the demo story.
6. **Hooks**: one React Query hook per endpoint (`useSummary(month)` → `useQuery({ queryKey: ['summary', month], queryFn: ... })`). Mutations (`useAffordability`, `useSaveGoal`) use `useMutation` and invalidate the queries they affect.

**Done when:** a test page can render `useSummary` data from the mocks.

---

## Phase 2: App shell and routing (≈ 1 h)

1. **Routes** in `App.tsx`:
   ```
   /onboarding           Onboarding (no shell)
   /            → AppShell
       index             Home
       wrapped           Wrapped (month list)
       wrapped/:month    Wrapped story player (full screen, hides nav)
       chat              Chat
       goals             Goals
       settings          Settings
   ```
   Redirect to `/onboarding` if there's no imported data. A `settings.onboarded` flag in mocks for now.
2. **`AppShell.tsx`** with the exact class names from `index.css`:
   ```tsx
   <div className="app-shell">
     <header className="app-bar">…logo, language, avatar…</header>
     <nav className="app-nav" aria-label="Main">
       <div className="app-nav__brand">CashCoach</div>
       <NavLink to="/" end className="app-nav__item"><Home/><span className="app-nav__label">{t.nav.home}</span></NavLink>
       …Wrapped, Chat, Goals…
       <div className="app-nav__footer">{t.disclaimer}</div>
     </nav>
     <main className="app-main" id="main"><div className="container"><Outlet/></div></main>
   </div>
   ```
   `NavLink` sets `aria-current="page"`, so the active-tab styling works with no extra code. Add a `.skip-link` to `#main`.

**Done when:** tabs switch pages and the nav is a bottom bar at 375px, a rail at 768px and a sidebar at 1280px.

---

## Phase 3: Shared UI components (≈ 2 h)

Build these once and use them everywhere. Each one should be small and use only tokens and classes from `index.css`.

| Component | Notes |
|---|---|
| `MoneyText` | `value: Money`, `variant: 'default'|'hero'`, `tone: 'auto'|'positive'|'alert'`. Uses `.money` classes and `formatMoney` |
| `StatTile` | label + `MoneyText` + optional delta badge. Lives in `.grid-stats` |
| `AiText` | `.ai-text` + `.badge--ai` "✨ AI" + "Verified ✓" when `factCheck === 'passed'` |
| `EvidenceButton` + `EvidenceDrawer` | Button `ⓘ` with `aria-label`. Opens the drawer with transactions + calculation. Drawer is built on `Sheet` |
| `Sheet` | `.sheet-backdrop` + `.sheet` + `.sheet__handle`. Closes on Esc and backdrop click, focus moves into it, `role="dialog" aria-modal="true"`. Render via `createPortal(…, document.body)` |
| `VerdictCard` | 🟢🟡🔴 icon + **word** + color (never color alone) |
| `ProgressBar` / `ProgressRing` | Bar: a div with a width %. Ring: an SVG circle with `stroke-dasharray` |
| `Chip`, `Skeleton`, `EmptyState` | Small helpers |

**Done when:** a temporary `/dev/ui` route shows every component in light and dark mode.

---

## Phase 4: Screens (in demo priority order)

### 4.1 Home (P0, ≈ 3 h)
Order: `SafeToSpendHero` → `.grid-stats` of 4 `StatTile`s → `InsightCard` (`AiText` + evidence) → `QuickAfford` → `CategoryBreakdown` (hand-written SVG donut in `SpendingChart.tsx`, colors from `--color-cat-*`) → `RecurringList` → `SavingsCard` ×3.
- Layout: phone stacks everything. Desktop uses `.split` (left: insight, chart · right: afford, recurring, savings).
- **AffordSheet:** `useAffordability` mutation → `VerdictCard` + breakdown table + editable assumptions. Changing an assumption re-runs the mutation (debounce 300 ms). "Make it a goal" navigates to `/goals?new=1&name=…&amount=…`.
- The donut is plain SVG (no chart library): segments are `<circle>`s with `stroke-dasharray`, and `style={{ stroke: 'var(--color-cat-…)' }}` picks up the tokens in light and dark mode.

### 4.2 Wrapped (P1, but the demo "wow", ≈ 3 h)
- **Month list:** `.grid-auto` of gradient tiles, linking to `/wrapped/2026-08`.
- **StoryPlayer:** a full-screen fixed overlay (`z-index: var(--z-sheet)`):
  - state `index`, and `cards = buildCards(wrappedData)` (array of components);
  - progress segments at the top (one `ProgressBar` per card, the current one animating over about 6s);
  - tap the right 2/3 for next, the left 1/3 for previous; ←/→/Esc on the keyboard; swipe via pointer events (dx > 50px);
  - pause on hold (`pointerdown` → pause, `pointerup` → resume);
  - auto-advance with a `setTimeout` reset on each index change. **Respect `prefers-reduced-motion`:** no auto-advance and no animations.
- **Cards:** one component per card (Intro, TotalSpent, TopMerchant, BiggestChange, Personality, Outro). Each has a bold gradient background, a huge number via `MoneyText variant="hero"`, an AI caption via `AiText` and `ⓘ` evidence.
- **Desktop:** the card is centered with `max-width: 420px; aspect-ratio: 9/16`, plus ← → buttons beside it.
- **Share (P2):** `toPng(cardRef.current)` from `html-to-image`, then `navigator.share({ files })` with a download fallback.

### 4.3 Chat (P1, ≈ 2.5 h)
- `useChat()` keeps `messages` in state and sends `POST /chat`, reading the **SSE stream**. You can't use `EventSource` for POST, so use `fetch` + `response.body.getReader()` + `TextDecoder`, split on `\n\n`, and parse the `event:` and `data:` lines (`delta`, `tool`, `evidence`, `done`, `error`; see root `docs/API.md`).
- Mock: `mockChatStream.ts` yields words every 40 ms, so you can build the UI before the backend exists.
- UI: `SuggestionChips` (empty state), `MessageBubble` (user navy, assistant `--color-ai-soft`), evidence chips under assistant messages, typing indicator, auto-scroll to bottom (`ref.scrollIntoView` on new delta).
- `ChatInput`: pinned at the bottom. On phones, offset it above the tab bar: `bottom: calc(var(--tab-bar-height) + var(--safe-bottom))`. Enter sends, Shift+Enter adds a newline.

### 4.4 Goals (P1, ≈ 2.5 h)
- `useGoals()` list, then `GoalCard` in `.grid-auto`: emoji, `ProgressRing`, saved/target, deadline, required per week or month, status badge, optional AI tip with [Apply].
- `GoalSheet` (create/edit): a controlled form. The required-per-week figure and the verdict come from the backend (`POST /goals/preview`). Debounce while typing. **Don't calculate in the frontend.**
- Pre-fill from query params when coming from "Make it a goal".
- Empty state: "Set your first goal" + a suggested "Emergency fund".

### 4.5 Onboarding (P0, ≈ 1.5 h)
3 steps with `StepDots`: language → payday and balance → `TransactionUpload` (drag & drop + file input, `multipart/form-data` to `/transactions/import`) → import result ("312 transactions · 37 by AI · 4 to review") → go to Home.

### 4.6 Settings (P2, ≈ 30 min)
Language, payday, buffer, theme (`document.documentElement.dataset.theme = 'dark'|'light'`, saved in localStorage), and "Delete all my data".

---

## Phase 5: Polish (≈ 2 h, Design is 20% of the score)
- Skeletons for every query's loading state. `EmptyState` and error state with "Try again".
- Check at **375 / 768 / 1280 / 1920** px, light and dark, and keyboard-only (Tab through each screen).
- Micro-interactions: card press scale, sheet slide, number count-up on the Home hero and Wrapped (skip when reduced motion is on).
- Real copy in Polish (proofread) and the "Not financial advice" note in the nav footer.
- Lighthouse: aim for Accessibility ≥ 95.

---

## Phase 6: Switch to the real API
1. Set `VITE_USE_MOCKS=false`.
2. Fix type mismatches. Keep `types/index.ts` and root `docs/API.md` in sync.
3. Test the full demo flow from `docs/DEMO_SCRIPT.md` with `data/samples/transactions_mbank.csv`.

---

## Backend contract additions (tell the backend person)
Home, Chat and Afford are already in root `docs/API.md`. **Wrapped and Goals are new.** Agree on these shapes and add them to `docs/API.md`:

```jsonc
// GET /api/wrapped?month=2026-08
{
  "month": "2026-08",
  "totalSpent": "2875.40", "changePct": 9.0, "transactionCount": 312,
  "topMerchant": { "name": "Glovo", "count": 14, "amount": "412.30", "evidence": {…} },
  "topCategory": { "category": "rent_bills", "amount": "1100.00", "sharePct": 38.0 },
  "biggestChange": { "category": "restaurants_cafes", "from": "98.00", "to": "161.00", "changePct": 64.0 },
  "cheapestWeekday": "tuesday",
  "potentialSavings": "309.00",
  "personality": { "emoji": "🌙", "title": "The Weekend Foodie", "description": "…", "aiGenerated": true },
  "captions": { "totalSpent": "…", "topMerchant": "…" }   // AI, fact-checked
}

// GET /api/goals · POST /api/goals · PUT /api/goals/{id} · DELETE /api/goals/{id}
{ "id": "g1", "name": "Concert in Gdańsk", "emoji": "🎸",
  "target": "1200.00", "saved": "540.00", "deadline": "2026-09-20",
  "requiredPerWeek": "165.00", "status": "on_track" | "behind" | "done",
  "aiTip": { "text": "…", "aiGenerated": true } }

// POST /api/goals/preview   (live calculation while typing; nothing saved)
{ "target": "1200.00", "saved": "0", "deadline": "2026-09-20" }
→ { "requiredPerWeek": "165.00", "verdict": "yellow", "plan": [{ "savingId": "s_delivery", "monthlyImpact": "160.00" }], "aiText": "…" }
```

---

## Suggested order and time budget
| # | Work | Priority | Est. |
|---|---|---|---|
| 1 | Phase 0–2: setup, foundations, shell | P0 | 3 h |
| 2 | Phase 3: shared UI | P0 | 2 h |
| 3 | Home + AffordSheet | P0 | 3 h |
| 4 | Onboarding | P0 | 1.5 h |
| 5 | Wrapped | P1 | 3 h |
| 6 | Chat | P1 | 2.5 h |
| 7 | Goals | P1 | 2.5 h |
| 8 | Polish + real API | P0 | 3 h |

If time runs short, cut in this order: Wrapped share → Settings → Goals AI tip → Chat tool indicator.

## Common pitfalls
- Money is a **string**. Never `+` or `-` it in the frontend. Format it for display only.
- Keep `ⓘ` buttons ≥ 44px tap area (pad them even if the icon is small).
- The Sheet and StoryPlayer must lock body scroll while open (`document.body.style.overflow = 'hidden'`) and restore it on close.
- React 19 StrictMode runs effects twice in dev. Make the SSE reader and timers clean up in the effect's return.
- Use `100dvh`, not `100vh`, for the full-screen story player (mobile browser bars).
