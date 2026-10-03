# Screens: low-fi sketches

Four main tabs: **Home · Wrapped · Chat · Goals**. Settings sits behind the avatar in the app bar. Onboarding is outside the tab shell.

Sketches are phone-first (375px). Desktop notes show what changes at ≥ 1024px (see breakpoints in [DESIGN.md](DESIGN.md)). UI copy ships in Polish and English; the sketches use English labels.

Legend: `[ Button ]` · `( chip )` · `✨` AI-written · `ⓘ` opens Evidence drawer · `▓▓░░` progress · `═══` divider

---

## 0. App shell & navigation

```
Phone                                  Desktop (≥1024px)
┌──────────────────────────────────┐   ┌────────────┬──────────────────────────────┐
│ CashCoach              (PL) (◉)  │   │ CashCoach  │                              │
├──────────────────────────────────┤   │            │                              │
│                                  │   │ ⌂ Home     │        page content          │
│          page content            │   │ ★ Wrapped  │        .container            │
│                                  │   │ 💬 Chat     │                              │
│                                  │   │ ◎ Goals    │                              │
├──────────────────────────────────┤   │            │                              │
│  ⌂ Home  ★ Wrapped  💬 Chat  ◎ Goals│   │ Not financial advice                    │
└──────────────────────────────────┘   └────────────┴──────────────────────────────┘
```
Tablet: same as desktop but the sidebar is an icon-only rail.

---

## 1. Home: "How am I doing right now?"

Purpose: one glance → how much I can safely spend until payday, what changed, and what to do next.

```
┌──────────────────────────────────┐
│ Hi Ola 👋        ‹ August 2026 › │  ← month switcher
├──────────────────────────────────┤
│ ┌──────────────────────────────┐ │
│ │ Safe to spend until 10.09  ⓘ │ │  ← HERO (deterministic number)
│ │        940,55 zł             │ │     .money--hero
│ │ ▓▓▓▓▓▓▓▓▓▓░░░░  12 days left │ │
│ └──────────────────────────────┘ │
│ ┌─────────────┐ ┌──────────────┐ │
│ │ Income      │ │ Spent        │ │  ← .grid-stats (2×2 on phone)
│ │ 3 200 zł    │ │ 2 875 zł ▲9% │ │
│ ├─────────────┤ ├──────────────┤ │
│ │ Saved       │ │ Savings found│ │
│ │ 325 zł      │ │ 309 zł/mo    │ │
│ └─────────────┘ └──────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ ✨ AI · This month           │ │  ← .ai-text, factCheck ✓
│ │ "Food delivery jumped to     │ │
│ │ 412 zł (14 orders) ⓘ. Rest   │ │
│ │ is close to July."           │ │
│ │                  Verified ✓  │ │
│ └──────────────────────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ Can I afford…?               │ │  ← quick ask → opens Afford sheet
│ │ [ Concert tickets ][ 1200 ]  │ │
│ │               [ Check → ]    │ │
│ └──────────────────────────────┘ │
│ Where it went            See all │
│ ┌──────────────────────────────┐ │
│ │   ◔ donut    ■ Rent 1100     │ │  ← CategoryBreakdown + SpendingChart
│ │              ■ Food   412    │ │     tap row → transactions list
│ │              ■ Groc.  380    │ │
│ └──────────────────────────────┘ │
│ Recurring (5)            62 zł/mo│
│  Spotify  23,99 · 05.09       ⓘ  │
│  Netflix  43,00 · 12.09       ⓘ  │
│ Top savings                       │
│ ┌──────────────────────────────┐ │
│ │ ✨ Cancel 2nd cloud plan     │ │  ← SavingsCard ×3
│ │ −9,99 zł/mo · easy  [✕] ⓘ    │ │
│ └──────────────────────────────┘ │
└──────────────────────────────────┘
```
**Desktop:** row 1 = hero + 4 stats (`.grid-stats` 4 across). Row 2 = `.split`: left (AI insight, chart), right (Can I afford, recurring, savings).

**Afford sheet** (opens from "Check →"):
```
┌──────────────────────────────────┐
│ ━━                               │
│ Concert tickets · 1 200 zł       │
│ ┌──────────────────────────────┐ │
│ │ 🟡 POSSIBLE, BUT TIGHT       │ │  ← icon + word + color
│ │ short by 259,45 zł           │ │
│ └──────────────────────────────┘ │
│ Balance              1 840,55    │
│ Bills before payday   −600,00 ⓘ │
│ Safety buffer         −300,00    │
│ ═══════════════════════════════  │
│ Safe to spend           940,55   │
│ ✨ "Buy after payday on the 10th │
│ and skip 2 deliveries → 🟢"      │
│ Assumptions ▾  payday [10]       │  ← editable → recalculates
│                buffer [300]      │
│ [ Make it a goal ]  [ Close ]    │  ← → Goals
└──────────────────────────────────┘
```

---

## 2. Wrapped: "My month, story-style" (like Spotify Wrapped)

Purpose: make the monthly recap fun and shareable for 18–26-year-olds. **Big innovation and design moment for the demo.**
Full-screen story cards, tap or swipe to advance, progress segments on top. Each number is computed; the AI writes the captions and the "money personality".

```
 Card 1 (intro)          Card 2                  Card 3
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│ ▬▬ ── ── ── ── ✕ │   │ ▬▬ ▬▬ ── ── ── ✕ │   │ ▬▬ ▬▬ ▬▬ ── ── ✕ │
│                  │   │                  │   │                  │
│   YOUR AUGUST    │   │  You spent       │   │  Your #1 place   │
│     WRAPPED      │   │                  │   │                  │
│       ★          │   │   2 875 zł       │   │     🛵 Glovo     │
│                  │   │                  │   │  14 orders       │
│  312 transactions│   │  ▲ 9% vs July    │   │  412 zł          │
│  31 days         │   │  ✨ "Not bad —   │   │  ✨ "That's one  │
│                  │   │  rent is 38%."   │   │  every 2 days 🍕"│
│   tap to start → │   │              ⓘ   │   │              ⓘ   │
└──────────────────┘   └──────────────────┘   └──────────────────┘

 Card 4                  Card 5                  Card 6 (outro)
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│ ▬▬ ▬▬ ▬▬ ▬▬ ── ✕ │   │ ▬▬ ▬▬ ▬▬ ▬▬ ▬▬ ✕ │   │ ▬▬ ▬▬ ▬▬ ▬▬ ▬▬ ✕ │
│  Biggest change  │   │ Your money       │   │  Next month you  │
│                  │   │ personality      │   │  could save      │
│  ☕ Coffee       │   │                  │   │                  │
│  ▲ +64%          │   │  🌙 "The Weekend │   │   309 zł         │
│  98 → 161 zł     │   │     Foodie"      │   │                  │
│                  │   │ ✨ 3-line AI     │   │ [ See how → ]    │  → Home savings
│ Best day: Tue    │   │ description      │   │ [ Share ↗ ]      │  → PNG of card
│ (lowest spend)   │   │                  │   │                  │
└──────────────────┘   └──────────────────┘   └──────────────────┘
```
**Above the stories** (the Wrapped tab landing): a list of available months as big gradient tiles ("August 2026 ★ New", "July 2026"), tap to play.
**Desktop:** cards are centered at phone size (max 420×740) on a dimmed backdrop, with ← → buttons beside them. Keyboard arrows work.

---

## 3. Chat: "Ask about my money"

```
┌──────────────────────────────────┐
│ Coach ✨                 (New ↺) │
├──────────────────────────────────┤
│  Try asking:                     │  ← empty state only
│  ( How much on Bolt in Aug? )    │
│  ( Can I save 500 zł by June? )  │
│  ( What do I spend on weekends?) │
│                                  │
│              ┌─────────────────┐ │
│              │ Ile wydałam na  │ │  ← user bubble (navy)
│              │ Bolta w sierpniu│ │
│              └─────────────────┘ │
│ ┌────────────────────────────┐   │
│ │ ✨ W sierpniu 186 zł na    │   │  ← assistant bubble (violet tint)
│ │ Bolta — 9 przejazdów, …    │   │     streams token by token
│ │ 🔎 used: transactions      │   │  ← tool used (small, muted)
│ │ ( 9 transactions ⓘ )       │   │  ← evidence chip → drawer
│ └────────────────────────────┘   │
│ ┌────────────────────────────┐   │
│ │ ● ● ●                      │   │  ← typing indicator
│ └────────────────────────────┘   │
├──────────────────────────────────┤
│ [ Ask anything…          ] [➤]  │  ← pinned above tab bar
└──────────────────────────────────┘
```
**Desktop:** `.container--narrow` centered column; suggestion chips stay visible on the side.

---

## 4. Goals: "Save for things I want"

```
┌──────────────────────────────────┐
│ Goals                  [ + New ] │
├──────────────────────────────────┤
│ ┌──────────────────────────────┐ │
│ │ 🎸 Concert in Gdańsk         │ │
│ │  ◔ 45%   540 / 1 200 zł      │ │  ← progress ring
│ │  by 20.09 · 165 zł/week      │ │
│ │  🟢 On track            ⓘ    │ │  ← status computed
│ └──────────────────────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ 🏖️ Summer trip              │ │
│ │  ◔ 12%   180 / 1 500 zł      │ │
│ │  by 30.06 · 140 zł/mo        │ │
│ │  🟡 Behind by 60 zł          │ │
│ │ ✨ "Skip 2 Glovo orders a    │ │
│ │ month to catch up." [Apply]  │ │
│ └──────────────────────────────┘ │
│ ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐ │
│   + Emergency fund (suggested)   │  ← suggestion if none exists
│ └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘ │
└──────────────────────────────────┘
```
**New / edit goal sheet:**
```
┌──────────────────────────────────┐
│ ━━  New goal                     │
│ Name      [ Concert in Gdańsk  ] │
│ Emoji     (🎸)(🏖️)(💻)(🎓)(🚗)   │
│ Amount    [ 1200          zł  ]  │
│ Deadline  [ 20.09.2026        ]  │
│ Saved now [ 0             zł  ]  │
│ ┌──────────────────────────────┐ │
│ │ Needs 165 zł/week            │ │  ← live, deterministic
│ │ 🟡 Tight with current habits │ │
│ │ ✨ Plan: cut delivery −160,  │ │
│ │ cancel 2nd cloud −10 …  ⓘ   │ │
│ └──────────────────────────────┘ │
│             [ Cancel ] [ Save ]  │  ← .actions
└──────────────────────────────────┘
```
**Desktop:** goal cards in `.grid-auto`. The sheet becomes a right side panel.

---

## Shared pieces seen across screens
`MoneyText` · `StatTile` · `AiText` (✨ + verified ✓) · `EvidenceButton ⓘ` + `EvidenceDrawer` · `Sheet` · `VerdictCard` · `ProgressRing` · `ProgressBar` · `Chip` · `Skeleton` · `EmptyState`
