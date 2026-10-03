# Frontend: React + TypeScript (Vite)

## Stack
React 18+, TypeScript (strict), Vite, Tailwind CSS, Recharts (charts), TanStack Query (server state), React Router.

## Structure
- `src/api/client.ts`: the only place that calls `fetch`. Typed functions per endpoint in `docs/API.md`. Base URL from `import.meta.env.VITE_API_URL` (set in the root `.env`; see `.env.example`). Vite looks for `.env` in `frontend/` by default, so `vite.config.ts` must set `envDir: '..'`.
- The frontend **never** calls Gemini and never reads `GEMINI_*` variables. All AI goes through the backend API. Anything prefixed `VITE_` is bundled into public JS, so never put a secret there.
- `src/types/index.ts`: TS types mirroring backend DTOs. Keep them in sync with `docs/API.md`.
- `src/hooks/`: TanStack Query hooks (`useTransactions`, ...). Components never call `api/` directly.
- `src/pages/`:
  - `Onboarding`: language, payday, CSV upload
  - `Dashboard`: summary, chart, savings
  - `Afford`: "Can I afford this?"
  - `Chat`
- `src/components/`: presentational components. `EvidenceDrawer` is shared: any AI claim must be able to open it.
- `src/index.css`: reset, design tokens (CSS variables) for color, type, spacing, radius, shadow and motion, light and dark themes, global element styles, and shared utilities (`.card`, `.btn`, `.money`, `.badge`, `.ai-text`, `.skeleton`). Components use `var(--token)`, never raw hex values.
- `docs/`: frontend-only plans. **Read `docs/DESIGN.md` before building any UI.** Put new frontend plans there, not in the root `docs/`.

## UX rules (Design counts for 20% of the score)
- Mobile-first. The target user is 18–26 and on a phone. It has to look good at 375px wide.
- Every AI-generated sentence that contains a number has a "Why?" / "Skąd to wiem?" affordance that opens `EvidenceDrawer` with the source transactions and the calculation.
- Show the AI vs. computed distinction visually, e.g. a subtle ✨ badge on AI text and plain numbers in tabular-nums.
- Affordability verdict: big green, yellow or red card, then the breakdown, then the editable assumptions.
- Loading states: skeletons, and streaming text for chat. Never a blank screen.
- Copy is in Polish and English. Keep strings in one place (simple dictionary object, no i18n library unless needed).
- Persistent "To nie jest porada finansowa / Not financial advice" footer note.

## Conventions
- Function components and hooks only. Named exports.
- Format money with `Intl.NumberFormat('pl-PL', { style: 'currency', currency: 'PLN' })`. The API sends amounts as decimal strings, so never do float math on them in the UI.
- No business calculations in the frontend. Display what the API returns.
- Accessibility: semantic HTML, labelled inputs, sufficient contrast, and color is never the only signal (verdicts also get an icon and a word).

## Commands
```bash
npm install
npm run dev
npm run build
npm run lint
```
