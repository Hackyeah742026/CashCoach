# Demo Script (≈ 3 minutes)

A concrete use case that makes the value easy to understand, as the brief requires. Uses the synthetic file `data/samples/transactions_mbank.csv`.

## Persona
**Ola, 21, student in Kraków.** Works part-time at a café (*umowa zlecenie*, about 2,400 zł/month) and gets 800 zł/month from family. Shares a flat (rent share 1,100 zł). Banks with mBank. Payday is the 10th. Ola feels the money "just disappears" and wants to go to a concert in Gdańsk with friends: tickets plus train come to 1,200 zł.

## Run of show

### 0:00 – 0:20 · Hook
> "Ola earns 3,200 zł a month and by the 25th has 40 zł left. The banking app shows a pie chart. CashCoach tells Ola *why*, and *what to do about it*."

### 0:20 – 0:50 · Import
1. Open the app on a phone-sized screen and pick Polski.
2. Upload `transactions_mbank.csv` (3 months).
3. Show the import result: **312 transactions · 271 by rules · 37 by AI · 4 to review.**
4. Open "4 to review" and fix one ("ZABKA Z3423" → Groceries, apply to merchant). *Point: the user is in control and the correction becomes a rule.*

### 0:50 – 1:40 · Where did the money go?
1. Dashboard: monthly chart plus the AI headline.
   > "August: you spent 2,875 zł, 9% more than in July. The biggest change is food delivery: 412 zł, 14 orders."
2. Tap **"Skąd to wiem?"**. The Evidence drawer lists the 14 Glovo/Pyszne transactions and the sum. *Point: AI explains, code calculates. Every number is verifiable.*
3. Recurring payments: Spotify, Netflix, **two cloud storage plans** (duplicate), phone, gym.

### 1:40 – 2:20 · Savings
Three cards with computed monthly impact:
- Duplicate cloud storage: **−9.99 zł/month** · easy
- Cook twice a week instead of delivery: **≈ −160 zł/month** · medium
- Unused gym (no Multisport check-ins visible, 3 months): **−139 zł/month** · easy

Total ≈ **309 zł/month**. Dismiss one ("I actually use the gym") to show control.

### 2:20 – 2:50 · Can I afford it?
Type: *"Bilety na koncert + pociąg do Gdańska, 1200 zł, 20 września"*.
- Verdict **🟡 Possible, but tight**: safe-to-spend 940 zł, short by 260 zł.
- Breakdown: balance 1,840 zł − bills before payday 600 zł − buffer 300 zł.
- AI tip: "Buy the train ticket after payday on the 10th and skip 2 deliveries, and you'll be green."
- Change the buffer to 150 zł and the verdict recalculates live. *Point: assumptions are editable.*

### 2:50 – 3:00 · Close
> "CashCoach: AI that explains your money, with every number checkable. Built for the first years of financial independence."

## Backup plan
- If the AI API is slow or down, the app falls back to templated text. Demo the evidence drawer and the affordability math, which are fully deterministic.
- Keep a pre-imported database (`cashcoach-demo.db`) and a screen recording ready.

## Chat questions to try live
- "Ile wydałam na Bolta w sierpniu?"
- "Can I save 500 zł for summer by June?"
- "Na co wydaję najwięcej w weekendy?"
