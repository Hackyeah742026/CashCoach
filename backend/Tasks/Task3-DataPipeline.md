# Task 3: Data pipeline (PDF B3 to B7, B9, B11, B12, B22)

Goal: import a bank CSV, clean it, categorize it, detect recurring payments, and expose it through the API.

## Steps
1. `Normalizer` (Core)
   - Uppercase, strip Polish diacritics.
   - Remove prefixes: BLIK, PAYU*, PAYPRO*, SUMUP*, KLARNA*, GOOGLE *, APPLE.COM/BILL.
   - Regex noise removal: store codes, card refs, long digit runs, dates, city list.
   - Keep first 1-2 tokens as the merchant key.
   - Flag BNPL: KLARNA, PAYPO, TWISTO, RATA, RAT, `x/y` patterns.
2. `Categorizer` (Core)
   - Exact match in `merchants.json` (about 150 Polish merchants).
   - Fuzzy match with `FuzzySharp` `Fuzz.TokenSetRatio >= 85`.
   - Batched LLM fallback (up to 50 keys, JSON mode, results saved back to the dictionary). Wire to the real client in Task 5; use a stub until then.
   - Anything unknown becomes `other`. User correction updates a per-user override.
3. `RecurringDetector` (Core)
   - Group by merchant; recurring if at least 2 payments, interval 28-33 or 6-8 days, amount variation <= 10%.
   - Types: subscription, rent, bnpl, salary (with payday day of month).
   - Duplicate detection: music (Spotify, Tidal, YouTube Music, Apple Music) and video (Netflix, HBO Max, Disney+, Prime).
   - BNPL remaining instalments and next dates from `RATA 2/4` patterns.
4. `SyntheticDataGenerator` (Infrastructure, `Bogus`, fixed seed)
   - 3 personas (student, first_job, bnpl_heavy), 3 months each, 150-250 transactions.
   - Messy descriptions, salary on a fixed day, rent on the 1st, 3-5 subscriptions, 1-3 BNPL plans.
   - Output CSV `date;amount;description;currency` into `data/samples/` (synthetic only).
5. Endpoints
   - `POST /demo/login`, `GET /me`, `PATCH /me`, `POST /me/consent`
   - `POST /import` (multipart `file`, `;` delimiter, Polish number format, duplicate skipping)
   - `GET /transactions`, `PATCH /transactions/{id}` (with `apply_to_merchant`)
   - `GET /summary`
   - `GET /subscriptions`, `PATCH /subscriptions/{id}`, `GET /bnpl`
   - DTOs as records in `Contracts/`, matching the PDF JSON.

## Done when
- Normalizer unit tests pass on 20 real-style descriptions.
- At least 95% of synthetic rows get a non-`other` category.
- All planted subscriptions and BNPL plans are found in synthetic data.
- Demo login returns a user with data in the DB; filters and re-categorization work in Scalar.
