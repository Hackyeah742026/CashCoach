# Data Model

All domain types live in `backend/src/CashCoach.Core/Domain/`. Money is `decimal` and currency is PLN.

## Transaction
| Field | Type | Notes |
|---|---|---|
| `Id` | string | Stable hash of (date, amount, raw description, bank), used for de-duplication |
| `Date` | DateOnly | Operation date |
| `Amount` | decimal | Expense < 0, income > 0 |
| `Currency` | string | `PLN`. Other currencies are flagged and excluded from totals |
| `RawDescription` | string | As exported. **Never sent to the AI un-anonymized** |
| `Merchant` | string | Normalized (`GLOVO*ZAMOWIENIE KRAKOW` → `Glovo`) |
| `Category` | Category | Enum, see `docs/API.md` |
| `CategorySource` | enum | `Rule` · `Ai` · `User` |
| `Confidence` | double | 1.0 for Rule and User. Model-provided for Ai |
| `IsRecurring` | bool | Set by `RecurringPaymentDetector` |
| `ImportId` | string | Which upload it came from |

## Category
Enum (see the API doc), plus metadata: display names in PL and EN, color, icon, `IsEssential` (rent, groceries, transport) and `IsDiscretionary` (delivery, entertainment). The savings logic uses these.

## CategoryRule
| Field | Type | Notes |
|---|---|---|
| `Pattern` | string | Case-insensitive substring or regex on the normalized merchant |
| `Category` | Category | |
| `Source` | enum | `BuiltIn` (shipped dictionary) · `User` (from a correction). User rules win |

## RecurringPayment
| Field | Type |
|---|---|
| `Merchant` | string |
| `TypicalAmount` | decimal |
| `Period` | `Weekly` · `Monthly` · `Yearly` |
| `LastDate` / `NextExpectedDate` | DateOnly |
| `TransactionIds` | string[] |

## Budget / UserSettings
| Field | Type | Notes |
|---|---|---|
| `Language` | `pl` · `en` | |
| `Payday` | int (1–31) | Day of month income usually arrives. Auto-suggested from income transactions |
| `SafetyBuffer` | decimal | Default 300 zł |
| `CurrentBalance` | decimal | From the CSV's last balance, or entered by the user |

## SavingSuggestion
| Field | Type | Notes |
|---|---|---|
| `Id` | string | Candidate type + key, e.g. `sub_dup_spotify` |
| `Type` | enum | `UnusedSubscription` · `DuplicateSubscription` · `FrequentSmallPurchases` · `DeliveryVsGroceries` · `BnplUsage` · `CategorySpike` |
| `MonthlyImpact` | decimal | **Computed by code** |
| `Calculation` | string | Human-readable formula |
| `Title` / `Rationale` / `Difficulty` | string / string / enum | **Written by AI** (template fallback) |
| `Evidence` | Evidence | |
| `Dismissed` | bool | User control |

## AffordabilityResult
| Field | Type |
|---|---|
| `Verdict` | `Green` · `Yellow` · `Red` |
| `SafeToSpend` | decimal |
| `Shortfall` | decimal? |
| `Breakdown` | `BreakdownLine[]` (label, amount, transactionIds) |
| `Assumptions` | payday, buffer, balance, horizon date |
| `Explanation` | AI text + tips (optional, template fallback) |

## Evidence
Attached to every AI-produced item.
| Field | Type |
|---|---|
| `TransactionIds` | string[] |
| `FactKeys` | string[] (keys of computed facts, e.g. `cat.food_delivery.month`) |
| `Figures` | `{label, amount}[]` |
| `Calculation` | string? |

## Persistence (SQLite via EF Core)
Tables: `Transactions`, `CategoryRules`, `Settings` (single row), `DismissedSuggestions`, `Imports`. Insight and AI responses are cached in memory, keyed by `(month, dataVersion, language)`.
