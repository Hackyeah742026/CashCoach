# User Control, Verification & Limitations

The brief asks teams to explain "the capabilities and limitations of the solution, and how users can verify its outputs and remain in control." This is our answer.

## 1. Verifiability: every number has a source

| Mechanism | What the user sees |
|---|---|
| **Deterministic math** | All amounts come from tested C# code, not from the model. |
| **Evidence drawer** | Tap "Skąd to wiem? / Why?" on any AI sentence to see the exact transactions and the formula (e.g. `14 orders × 29.45 zł = 412.30 zł`). |
| **Fact-check layer** | Every amount in AI text is matched against computed facts. Mismatches are retried once, then replaced with a template. The UI shows a ✓ "verified" marker. |
| **AI vs. computed labels** | AI-written text has a ✨ badge. Raw numbers and tables are plain. |
| **Transparent verdicts** | The affordability verdict shows the full breakdown and assumptions, not just a color. |

## 2. Control: the user overrides the AI

- **Re-categorize** any transaction. Choose "apply to all from this merchant" to create a rule that beats the AI from then on.
- **Review queue:** low-confidence AI categories (< 0.6) are flagged "needs review" instead of being silently accepted.
- **Edit assumptions** (payday, safety buffer, current balance) and the verdict recalculates instantly.
- **Dismiss suggestions** that don't fit the user's life. They don't come back.
- **Language** toggle PL/EN.
- **Data ownership:** a delete-all-data button. Nothing is stored outside the local SQLite file.

## 3. Privacy
- No bank credentials. The user uploads a CSV they exported themselves.
- The anonymizer removes IBANs and account numbers, card numbers, phone numbers and personal names before anything goes to the AI.
- The AI provider receives merchant, category, amount and date only, as aggregated facts where possible.
- Synthetic data is used for every demo and test.

## 4. Capabilities
- Parses mBank and PKO BP CSV exports (Polish number, date and encoding formats).
- Categorizes about 85–90% of transactions by rules and the rest by AI, with confidence scores.
- Detects subscriptions and recurring bills.
- Monthly explanation with month-over-month changes and anomalies.
- Ranked, quantified savings suggestions.
- "Can I afford this?" for one-off and installment purchases.
- Chat with tool access to the user's own data.
- Polish and English.

## 5. Limitations (stated honestly)
| Limitation | Mitigation / next step |
|---|---|
| CSV only, no live sync | Open banking (PSD2 AIS provider) in the roadmap |
| Two banks supported in the MVP | Parser interface makes new banks about 1h of work each |
| AI may mis-categorize unusual merchants | Confidence threshold, review queue, user rules |
| Affordability is a projection from past patterns; it can't know irregular future costs | Assumptions are visible and editable. Safety buffer |
| Not licensed financial advice | Persistent disclaimer. No product, credit or investment recommendations |
| Requires internet for AI features | Graceful fallback: computed numbers plus templated text without AI |
| Fact-check catches wrong amounts, not wrong reasoning | Short, evidence-linked outputs. Reasoning limited to ranking and phrasing pre-computed candidates |
| Single user, local deployment | Multi-user auth is out of hackathon scope |

## 6. Responsible-AI rules baked into the prompts
- Never invent numbers. Use only the provided facts or tool results.
- No shaming language about spending.
- No recommendations of specific loans, BNPL, investments or financial products.
- When there are signs of debt stress (repeated BNPL, overdraft, payday loans), gently suggest free help: consumer ombudsman (*Miejski/Powiatowy Rzecznik Konsumentów*) or non-profit debt counselling.
