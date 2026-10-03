# Sample data

**All files here are synthetic.** They mimic the CSV export format of Polish banks and contain no real personal data. Never commit real bank statements.

| File | Format | Content |
|---|---|---|
| `transactions_mbank.csv` | mBank "Historia operacji" export: `;` separator, decimal comma, Windows-1250, metadata lines before the header | Demo persona "Ola", Jun–Aug 2026, about 300 rows |
| `transactions_pko.csv` | PKO BP export: `,` separator, quoted fields, UTF-8 | Same persona, smaller set, used to test the second parser |

Check the exact column layout against a real export from your own account, and copy only the **header structure**, never the data.

## What the demo data must contain (to match `docs/DEMO_SCRIPT.md`)
- Income: salary from the café on the 10th (about 2,400 zł) and a family transfer (800 zł)
- Rent share (1,100 zł) via transfer to a flatmate
- About 14 food delivery orders in August (Glovo, Pyszne.pl), totaling about 412 zł; fewer in June and July
- Subscriptions: Spotify, Netflix, phone (Play), gym (139 zł), **two cloud storage plans** (Google One + iCloud)
- Groceries: Biedronka, Lidl, Żabka
- Transport: Bolt, MPK Kraków ticket
- A few BLIK transfers to people, one Allegro purchase, one PayPo installment
- 3–5 deliberately unusual merchant names (for AI categorization and the review queue)
- Closing balance of 1,601.52 zł (bills before payday 360.97 zł, so safe-to-spend is 940.55 zł)
