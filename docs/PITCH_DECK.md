# Pitch Deck Outline (max 10 slides → PDF)

Each slide notes which judging criterion it targets: Idea & Innovation 30% · Relation to Category 20% · Usability 20% · Design 20% · Completeness 10%.

---

### 1. Title
**CashCoach: your AI money coach.** Team name, members, HackYeah 2026 · Open Task: AI. A single hero screenshot (dashboard on a phone).

### 2. Problem *(Usability)*
- Young Poles (18–26) manage money alone for the first time: zlecenia, stipends, shared rent, BLIK, BNPL.
- Banking apps show *what* happened, not *why*, or *what to do*.
- One relatable quote: "I earn 3,200 zł and by the 25th I have nothing left."
- _Add 1–2 stats with sources (e.g. on financial literacy or BNPL use among young people in Poland)._

### 3. Solution *(Innovation)*
Three questions CashCoach answers:
1. Where did my money go?
2. Where can I realistically save?
3. Can I afford this?

Upload a CSV → understandable answers in about 30 seconds, in Polish.

### 4. Demo: Ola's story *(Usability, Design)*
3–4 screenshots in sequence: import → insight with evidence → savings → affordability verdict.

### 5. How AI is used *(Relation to Category)*
Diagram: Rules → AI categorization → deterministic analysis → AI explanation → fact-check → user.
Table: *AI does / Code does / User controls* (from the README).

### 6. Trust: "AI explains, code calculates" *(Innovation, Category)*
- Every number is computed and tested, never generated.
- Fact-check layer and an Evidence drawer on every claim.
- User corrections become rules. Editable assumptions.
This slide answers the brief's "how can users verify outputs and remain in control?"

### 7. Architecture and technical decisions *(Completeness)*
React · ASP.NET Core · SQLite · Google Gemini (function calling, JSON outputs, streaming). Anonymizer before AI (GDPR). Why CSV rather than open banking for the MVP.

### 8. Design *(Design)*
Mobile-first UI, verdict cards, PL/EN, accessibility (color plus icon plus word). 2–3 polished screens.

### 9. Limitations and roadmap *(Completeness)*
Honest limits (CSV only, two banks, not financial advice). Next: PSD2 open banking, more banks, goals and savings pots, push nudges before payday, partnership with banks or student organizations.

### 10. Impact and ask
Who benefits, why now, what's built and working today. QR code to the demo and repo. "Thank you."

---

## Design notes for the PDF
- Same color tokens as the app (`frontend/src/styles/theme.css`).
- One idea per slide, large numbers, real screenshots (no mockups if possible).
- Export at 16:9 and check that the PDF has ≤ 10 pages.
