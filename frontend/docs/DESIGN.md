# Design Plan

Design is 20% of the score. Goal: **trustworthy like a bank, friendly like an app for 20-year-olds.**

## Direction
- **Mood:** calm, clear, optimistic. Money is stressful, so the UI must lower that stress. No red walls, no shaming.
- **Bank feel (PKO BP inspired):** a deep navy primary color is the visual language Polish users associate with PKO BP and with banking in general, so it gives instant trust. We use our own palette *inspired by* it. We never use the PKO logo, name or exact brand assets in the UI, because CashCoach is an independent app.
- **Young feel:** generous rounded corners, soft shadows, a fresh mint accent for "money saved", big numbers, short copy, emoji used sparingly as category icons.
- **AI is visible:** AI-written text has a violet accent and a ✨ badge, so users always know what was generated and what was calculated.

## Typography
| Role | Font | Why |
|---|---|---|
| Headings, big numbers | **Plus Jakarta Sans** (600–800) | Modern, geometric, friendly. Full Polish diacritics |
| Body, UI, tables | **Inter** (400–600) | Highly legible at small sizes on phones. Tabular numbers for money |

- Money always uses `font-variant-numeric: tabular-nums` so digits line up.
- Base size 16px (never smaller for body text on mobile). Fluid heading scale with `clamp()`.
- Polish copy can be about 20–30% longer than English, so never design for fixed-width text.

## Color
| Token group | Use |
|---|---|
| **Brand navy** (`--color-brand-*`, primary `#003574`) | Header, primary buttons, links, selected states |
| **Neutrals** (cool, blue-tinted greys) | Backgrounds, borders, secondary text |
| **Mint / success** | Savings, income, "green" verdict |
| **Amber / warning** | "Yellow" verdict, needs review |
| **Red / danger** | "Red" verdict, errors. Used sparingly |
| **Violet / AI** | AI badge, AI text accents, chat assistant bubbles |
| **Category palette** (8 colors) | Charts and category chips, distinguishable in both themes |

- Contrast: text meets WCAG AA (4.5:1). Verdicts are never color-only: they always get an icon and a word.
- Dark mode via `prefers-color-scheme`, plus a manual override `data-theme="light|dark"` on `<html>`.

## Layout & responsiveness
Mobile-first: write base styles for the phone, then add `min-width` queries. 4px spacing scale. Cards are the main building block (white surface, 16px radius, soft shadow). One primary action per screen.

| Breakpoint | Width | Navigation | Content | Drawer |
|---|---|---|---|---|
| **Phone** | < 600px | Top app bar + **bottom tab bar** | 1 column, full-width buttons | Bottom sheet |
| **Tablet** | 600–1023px | Left **icon rail** (88px) + top bar | 2 columns | Right side panel |
| **Laptop** | 1024–1439px | Full **sidebar** with labels (248px), no top bar | 3 columns, `.split` side by side | Right side panel |
| **Wide** | ≥ 1440px | Sidebar | Container 1280px, type 17px base, taller charts | Wider panel (480px) |

Also handled: phone landscape (slimmer bars, icon-only tabs), iPhone notch and home bar (safe-area insets), touch targets ≥ 44px on touch devices, hover effects only on real pointer devices, and tables that scroll horizontally on phones.

### Layout classes (in `src/index.css`)
| Class | Use |
|---|---|
| `.app-shell`, `.app-bar`, `.app-nav`, `.app-nav__item`, `.app-main` | App frame. Navigation changes shape per breakpoint automatically |
| `.container`, `.container--narrow` | Centered content with responsive gutters (narrow for Onboarding and forms) |
| `.page-header` | Title + actions. Stacked on phone, one row on tablet+ |
| `.grid-2` / `.grid-3` / `.grid-4` | 1 → 2 → N columns |
| `.grid-auto` (`--grid-min`) | As many columns as fit |
| `.grid-stats` | KPI tiles: 2 per row on phone, 4 on desktop |
| `.split` (`--even`, `__aside--sticky`) | Main + side column, stacked below 1024px |
| `.sheet`, `.sheet-backdrop`, `.sheet__handle` | Evidence drawer |
| `.table-scroll`, `.chart`, `.actions` | Responsive tables, chart heights, form buttons |
| `.hide-phone`, `.hide-tablet`, `.hide-desktop`, `.phone-only` | Visibility |

### Per screen
- **Home:** `.grid-stats` (income, expenses, saved, savings found) → `.split` (AI insight + chart | quick afford + recurring + savings). The Afford result opens in a `.sheet`.
- **Wrapped:** month tiles in `.grid-auto`. The story player is a full-screen overlay; on desktop the card is centered at phone size.
- **Chat:** `.container--narrow`, input pinned above the tab bar on phone.
- **Goals:** goal cards in `.grid-auto`. Create/edit in a `.sheet`.
- **Onboarding:** `.container--narrow`, centered card, no navigation.

Test at 375, 768, 1280 and 1920 px widths before demoing.

## Key screens
Tabs: **Home · Wrapped · Chat · Goals**, plus Onboarding and Settings. Low-fi sketches are in [SCREENS.md](SCREENS.md); the build order is in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).
1. **Home:** safe-to-spend hero → stats → AI insight (✨) → "Can I afford…?" (verdict sheet) → category donut → recurring → savings.
2. **Wrapped:** the monthly recap as story cards, like Spotify Wrapped: total, top merchant, biggest change, AI "money personality", shareable.
3. **Chat:** assistant bubbles in violet tint, user bubbles in navy, evidence chips under answers.
4. **Goals:** savings goals with a progress ring, required pace, status, and an AI catch-up tip.
5. **Onboarding:** 3 short steps (language → payday & balance → upload CSV).
6. **Evidence drawer:** bottom sheet on mobile, side panel on desktop. Lists transactions and the formula.

## Components (shared look)
Button (primary / secondary / ghost), Card, Badge (AI, category, verdict), Money (formatted, colored by sign), Input / Select, BottomSheet, Skeleton loader, Toast.

## Motion
150–250ms ease-out transitions on hover and press, sheets slide up, skeleton shimmer. Everything is disabled under `prefers-reduced-motion`.

## Implementation
All tokens and global styles live in `frontend/src/index.css` (CSS custom properties). Components use the tokens (`var(--color-primary)`) and never hardcode hex values.
