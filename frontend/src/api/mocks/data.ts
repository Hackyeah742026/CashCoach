// Synthetic demo data for persona "Ola" (docs/DEMO_SCRIPT.md). Not real people or accounts.
// Numbers are internally consistent: category totals = sum of transactions.

import type { Category, Language, Money, MonthKey, SavingSuggestion, Transaction, Weekday, Wrapped } from '../../types'

export const MOCK_TODAY = '2026-08-29'
export const MOCK_MONTHS: MonthKey[] = ['2026-06', '2026-07', '2026-08']
export const MOCK_LATEST_MONTH: MonthKey = '2026-08'

export const DEFAULT_SETTINGS = {
  payday: 10,
  safetyBuffer: '300.00',
  currentBalance: '1601.52',
  balanceIsEstimate: false,
  income: { status: 'unknown' as const, day: 10, dayRule: 'fixed_day' as const, amount: null, source: null },
}

// ---------------------------------------------------------------------------
// August transactions (used by Evidence drawer and chat)

function tx(
  id: string,
  date: string,
  amount: string,
  merchant: string,
  description: string,
  category: Category,
  opts: Partial<Pick<Transaction, 'categorySource' | 'confidence' | 'isRecurring'>> = {},
): Transaction {
  return {
    id,
    date: `2026-08-${date}`,
    amount,
    merchant,
    description,
    category,
    categorySource: opts.categorySource ?? 'rule',
    confidence: opts.confidence ?? 1,
    isRecurring: opts.isRecurring ?? false,
  }
}

const glovo: [string, string][] = [
  ['02', '27.90'], ['04', '31.50'], ['06', '29.00'], ['08', '33.40'], ['09', '25.80'], ['11', '30.10'], ['13', '28.70'],
  ['15', '32.20'], ['16', '26.90'], ['18', '29.90'], ['21', '31.10'], ['23', '27.40'], ['25', '30.80'], ['28', '27.60'],
]
const bolt: [string, string][] = [
  ['03', '18.00'], ['07', '22.00'], ['09', '19.50'], ['14', '24.00'], ['16', '21.00'], ['20', '17.50'], ['22', '23.00'], ['24', '20.00'], ['27', '21.00'],
]
const coffee: [string, string, string][] = [
  ['01', '18.50', 'Costa Coffee'], ['05', '22.00', 'Karma Kraków'], ['08', '16.90', 'Starbucks'], ['12', '24.50', 'Karma Kraków'],
  ['17', '19.00', 'Costa Coffee'], ['19', '21.60', 'Starbucks'], ['23', '17.50', 'Costa Coffee'], ['26', '21.00', 'Karma Kraków'],
]
const groceries: [string, string, string][] = [
  ['01', '45.20', 'Biedronka'], ['04', '62.80', 'Lidl'], ['06', '18.40', 'Żabka'], ['10', '51.30', 'Biedronka'], ['12', '9.99', 'Żabka'],
  ['15', '38.70', 'Lidl'], ['18', '55.10', 'Biedronka'], ['20', '12.50', 'Żabka'], ['24', '47.90', 'Lidl'], ['27', '38.61', 'Biedronka'],
]

export const MOCK_TRANSACTIONS: Transaction[] = [
  tx('t_i01', '10', '2400.00', 'Kawiarnia Ziarno', 'WYNAGRODZENIE UMOWA ZLECENIE 07/2026', 'income_salary', { isRecurring: true }),
  tx('t_i02', '01', '800.00', 'Przelew rodzinny', 'PRZELEW OD [osoba] KIESZONKOWE', 'income_other', { isRecurring: true }),
  ...glovo.map(([d, a], i) => tx(`t_g${String(i + 1).padStart(2, '0')}`, d, `-${a}`, 'Glovo', 'GLOVO*ZAMOWIENIE KRAKOW', 'food_delivery')),
  ...bolt.map(([d, a], i) => tx(`t_b${String(i + 1).padStart(2, '0')}`, d, `-${a}`, 'Bolt', 'BOLT.EU/O/2608 TALLINN', 'transport')),
  ...coffee.map(([d, a, m], i) =>
    tx(`t_c${String(i + 1).padStart(2, '0')}`, d, `-${a}`, m, `${m.toUpperCase()} KRAKOW`, 'restaurants_cafes', m === 'Karma Kraków' ? { categorySource: 'ai', confidence: 0.86 } : {}),
  ),
  ...groceries.map(([d, a, m], i) => tx(`t_r${String(i + 1).padStart(2, '0')}`, d, `-${a}`, m, `${m.toUpperCase()} 3423 KRAKOW`, 'groceries')),
  tx('t_s01', '05', '-23.99', 'Spotify', 'SPOTIFY P2F8A1 STOCKHOLM', 'subscriptions', { isRecurring: true }),
  tx('t_s02', '12', '-43.00', 'Netflix', 'NETFLIX.COM AMSTERDAM', 'subscriptions', { isRecurring: true }),
  tx('t_s03', '03', '-9.99', 'Google One', 'GOOGLE *GOOGLE ONE', 'subscriptions', { isRecurring: true }),
  tx('t_s04', '07', '-9.99', 'iCloud+', 'APPLE.COM/BILL ICLOUD', 'subscriptions', { isRecurring: true }),
  tx('t_s05', '01', '-139.00', 'CityFit', 'CITYFIT KARNET OPEN', 'subscriptions', { isRecurring: true }),
  tx('t_h01', '10', '-1100.00', 'Czynsz (współlokatorka)', 'PRZELEW DO [osoba] CZYNSZ 08', 'rent_bills', { isRecurring: true }),
  tx('t_h02', '08', '-35.00', 'Play', 'P4 SP. Z O.O. FAKTURA', 'rent_bills', { isRecurring: true }),
  tx('t_a01', '14', '-149.99', 'Allegro', 'ALLEGRO.PL ZAKUP', 'shopping'),
  tx('t_e01', '09', '-32.00', 'Cinema City', 'CINEMA CITY BONARKA', 'entertainment'),
  tx('t_e02', '22', '-57.00', 'Going.', 'GOING. BILETY', 'entertainment', { categorySource: 'ai', confidence: 0.74 }),
  tx('t_p01', '20', '-100.00', 'PayPo', 'PAYPO RATA 2/4', 'bnpl', { isRecurring: true }),
  tx('t_x01', '19', '-35.64', 'Rossmann', 'ROSSMANN 245 KRAKOW', 'health_beauty'),
]

export const IDS = {
  glovo: glovo.map((_, i) => `t_g${String(i + 1).padStart(2, '0')}`),
  bolt: bolt.map((_, i) => `t_b${String(i + 1).padStart(2, '0')}`),
  coffee: coffee.map((_, i) => `t_c${String(i + 1).padStart(2, '0')}`),
  subscriptions: ['t_s01', 't_s02', 't_s03', 't_s04', 't_s05'],
  billsBeforePayday: ['t_s05', 't_s03', 't_s01', 't_s04', 't_h02', 't_s02', 't_p01'],
}

// ---------------------------------------------------------------------------
// Monthly category totals (expenses, negative)

export const MONTHLY_CATEGORIES: Record<MonthKey, Partial<Record<Category, Money>>> = {
  '2026-06': {
    rent_bills: '-1135.00', groceries: '-410.00', food_delivery: '-280.00', subscriptions: '-225.97', transport: '-160.00',
    shopping: '-120.00', restaurants_cafes: '-110.00', entertainment: '-95.00', health_beauty: '-54.03',
  },
  '2026-07': {
    rent_bills: '-1135.00', groceries: '-400.50', food_delivery: '-307.70', subscriptions: '-225.97', transport: '-172.20',
    entertainment: '-120.00', bnpl: '-100.00', restaurants_cafes: '-98.00', shopping: '-64.99', health_beauty: '-13.64',
  },
  '2026-08': {
    rent_bills: '-1135.00', food_delivery: '-412.30', groceries: '-380.50', subscriptions: '-225.97', transport: '-186.00',
    restaurants_cafes: '-161.00', shopping: '-149.99', bnpl: '-100.00', entertainment: '-89.00', health_beauty: '-35.64',
  },
}

export const MONTHLY_TOTALS: Record<MonthKey, { income: Money; expenses: Money }> = {
  '2026-06': { income: '3200.00', expenses: '-2590.00' },
  '2026-07': { income: '3200.00', expenses: '-2638.00' },
  '2026-08': { income: '3200.00', expenses: '-2875.40' },
}

// ---------------------------------------------------------------------------
// AI texts (what the backend would return after fact-checking)

export const NARRATIVES: Record<MonthKey, Record<Language, { headline: string; bullets: { text: string; ids: string[]; keys: string[] }[] }>> = {
  '2026-08': {
    pl: {
      headline: 'Sierpień: wydatki 2 875,40 zł — o 9% więcej niż w lipcu.',
      bullets: [
        { text: 'Dostawy jedzenia wzrosły do 412,30 zł (14 zamówień w Glovo) — to największa zmiana.', ids: IDS.glovo, keys: ['cat.food_delivery.month'] },
        { text: 'Kawiarnie: 161,00 zł, o 64% więcej niż w lipcu.', ids: IDS.coffee, keys: ['cat.restaurants_cafes.month'] },
        { text: 'Subskrypcje kosztują 225,97 zł miesięcznie — masz dwa plany chmury (Google One i iCloud+).', ids: IDS.subscriptions, keys: ['cat.subscriptions.month'] },
      ],
    },
    en: {
      headline: 'August: you spent 2,875.40 zł — 9% more than in July.',
      bullets: [
        { text: 'Food delivery rose to 412.30 zł (14 Glovo orders) — your biggest change.', ids: IDS.glovo, keys: ['cat.food_delivery.month'] },
        { text: 'Cafés: 161.00 zł, 64% more than in July.', ids: IDS.coffee, keys: ['cat.restaurants_cafes.month'] },
        { text: 'Subscriptions cost 225.97 zł a month — you pay for two cloud plans (Google One and iCloud+).', ids: IDS.subscriptions, keys: ['cat.subscriptions.month'] },
      ],
    },
  },
  '2026-07': {
    pl: {
      headline: 'Lipiec: wydatki 2 638,00 zł — prawie tyle samo co w czerwcu.',
      bullets: [
        { text: 'Dostawy jedzenia: 307,70 zł, o 10% więcej niż w czerwcu.', ids: [], keys: ['cat.food_delivery.month'] },
        { text: 'Pierwsza rata PayPo: 100,00 zł.', ids: [], keys: ['cat.bnpl.month'] },
      ],
    },
    en: {
      headline: 'July: you spent 2,638.00 zł — about the same as June.',
      bullets: [
        { text: 'Food delivery: 307.70 zł, 10% more than in June.', ids: [], keys: ['cat.food_delivery.month'] },
        { text: 'First PayPo instalment: 100.00 zł.', ids: [], keys: ['cat.bnpl.month'] },
      ],
    },
  },
  '2026-06': {
    pl: {
      headline: 'Czerwiec: wydatki 2 590,00 zł. Zostało 610,00 zł z wpływów.',
      bullets: [{ text: 'Największa kategoria to czynsz i rachunki: 1 135,00 zł.', ids: [], keys: ['cat.rent_bills.month'] }],
    },
    en: {
      headline: 'June: you spent 2,590.00 zł and kept 610.00 zł of your income.',
      bullets: [{ text: 'Your biggest category was rent & bills: 1,135.00 zł.', ids: [], keys: ['cat.rent_bills.month'] }],
    },
  },
}

export function savingsFor(lang: Language): SavingSuggestion[] {
  const pl = lang === 'pl'
  return [
    {
      id: 's_cloud',
      title: pl ? 'Zrezygnuj z drugiego planu chmury' : 'Cancel your second cloud plan',
      rationale: pl
        ? 'Płacisz za Google One i iCloud+. Zostaw jeden — oba mają po 100 GB.'
        : 'You pay for both Google One and iCloud+. Keep one — both give you 100 GB.',
      monthlyImpact: '9.99',
      difficulty: 'easy',
      evidence: { transactionIds: ['t_s03', 't_s04'], calculation: 'iCloud+ 9,99 zł × 1 / mies.' },
    },
    {
      id: 's_delivery',
      title: pl ? 'Gotuj 2× w tygodniu zamiast Glovo' : 'Cook twice a week instead of ordering',
      rationale: pl
        ? '14 zamówień w sierpniu, średnio 29,45 zł. Ugotowanie podobnego posiłku to ok. 9,50 zł.'
        : '14 orders in August, 29.45 zł on average. Cooking a similar meal costs about 9.50 zł.',
      monthlyImpact: '160.00',
      difficulty: 'medium',
      evidence: { transactionIds: IDS.glovo, calculation: '8 × (29,45 − 9,50) = 159,60 ≈ 160 zł' },
    },
    {
      id: 's_gym',
      title: pl ? 'Sprawdź, czy korzystasz z siłowni' : 'Check if you still use the gym',
      rationale: pl
        ? 'Karnet CityFit 139 zł miesięcznie. Jeśli chodzisz rzadziej niż 4× w miesiącu, tańsze będą wejścia jednorazowe.'
        : 'CityFit costs 139 zł a month. If you go less than 4 times a month, single entries are cheaper.',
      monthlyImpact: '139.00',
      difficulty: 'easy',
      evidence: { transactionIds: ['t_s05'], calculation: 'CityFit 139,00 zł / mies.' },
    },
  ]
}

// ---------------------------------------------------------------------------
// Wrapped

interface WrappedSeed {
  totalSpent: Money
  changePct: number | null
  transactionCount: number
  topMerchant: { name: string; count: number; amount: Money; ids: string[] }
  topCategory: { category: Category; amount: Money; sharePct: number }
  biggestChange: Wrapped['biggestChange']
  cheapestWeekday: Weekday
  personality: { emoji: string; title: Record<Language, string>; description: Record<Language, string> }
  captions: Record<Language, Wrapped['captions']>
}

export const WRAPPED: Record<MonthKey, WrappedSeed> = {
  '2026-08': {
    totalSpent: '2875.40',
    changePct: 9,
    transactionCount: 55,
    topMerchant: { name: 'Glovo', count: 14, amount: '412.30', ids: IDS.glovo },
    topCategory: { category: 'rent_bills', amount: '1135.00', sharePct: 39 },
    biggestChange: { category: 'restaurants_cafes', from: '98.00', to: '161.00', changePct: 64 },
    cheapestWeekday: 'tuesday',
    personality: {
      emoji: '🌙',
      title: { pl: 'Weekendowy Smakosz', en: 'The Weekend Foodie' },
      description: {
        pl: 'W tygodniu trzymasz budżet, ale weekendy należą do jedzenia na wynos i kawy z przyjaciółmi. Smacznie — tylko pilnuj, żeby wystarczyło do 10.',
        en: 'You stick to a budget on weekdays, but weekends belong to takeaway and coffee with friends. Tasty — just make sure it lasts until the 10th.',
      },
    },
    captions: {
      pl: {
        totalSpent: 'Nieźle — czynsz to prawie 40% tej kwoty.',
        topMerchant: 'To jedno zamówienie co dwa dni 🍕',
        biggestChange: 'Kawa smakuje coraz lepiej… i coraz drożej ☕',
      },
      en: {
        totalSpent: 'Not bad — rent is almost 40% of that.',
        topMerchant: "That's one order every two days 🍕",
        biggestChange: 'Coffee keeps getting better… and pricier ☕',
      },
    },
  },
  '2026-07': {
    totalSpent: '2638.00',
    changePct: 2,
    transactionCount: 49,
    topMerchant: { name: 'Glovo', count: 10, amount: '307.70', ids: [] },
    topCategory: { category: 'rent_bills', amount: '1135.00', sharePct: 43 },
    biggestChange: { category: 'shopping', from: '120.00', to: '64.99', changePct: -46 },
    cheapestWeekday: 'monday',
    personality: {
      emoji: '🧘',
      title: { pl: 'Spokojny Planista', en: 'The Calm Planner' },
      description: {
        pl: 'Stabilny miesiąc bez szaleństw. Mniej zakupów online, więcej kontroli.',
        en: 'A steady month with no surprises. Less online shopping, more control.',
      },
    },
    captions: {
      pl: { totalSpent: 'Prawie tyle samo co w czerwcu — stabilnie.', topMerchant: '10 zamówień — w normie.', biggestChange: 'Zakupy online o połowę w dół 👏' },
      en: { totalSpent: 'Almost the same as June — steady.', topMerchant: '10 orders — about normal.', biggestChange: 'Online shopping halved 👏' },
    },
  },
}

// ---------------------------------------------------------------------------
// Chat

export interface ChatScript {
  match: RegExp
  tool: string
  answer: Record<Language, string>
  transactionIds: string[]
  figures?: { label: Record<Language, string>; amount: Money }[]
}

export const CHAT_SCRIPTS: ChatScript[] = [
  {
    match: /bolt/i,
    tool: 'get_transactions',
    answer: {
      pl: 'W sierpniu na Bolta poszło 186,00 zł — 9 przejazdów, średnio 20,67 zł. Najczęściej w piątki i soboty wieczorem. Gdyby 3 z nich zastąpić biletem MPK (6 zł), zostałoby ok. 44 zł.',
      en: 'In August you spent 186.00 zł on Bolt — 9 rides, 20.67 zł on average, mostly on Friday and Saturday evenings. Swapping 3 of them for an MPK ticket (6 zł) would save about 44 zł.',
    },
    transactionIds: IDS.bolt,
    figures: [{ label: { pl: 'Bolt, sierpień 2026', en: 'Bolt, August 2026' }, amount: '-186.00' }],
  },
  {
    match: /500|zaoszcz|save/i,
    tool: 'get_category_summary',
    answer: {
      pl: 'Do czerwca zostało ok. 43 tygodnie, więc 500 zł to niecałe 12 zł tygodniowo. Najprościej: zrezygnuj z drugiego planu chmury (9,99 zł/mies.) i zamów jedno jedzenie mniej w miesiącu (ok. 29 zł). Chcesz, żebym zrobił z tego cel?',
      en: "There are about 43 weeks until June, so 500 zł is under 12 zł a week. The easiest way: cancel your second cloud plan (9.99 zł/month) and order one fewer meal a month (about 29 zł). Want me to turn this into a goal?",
    },
    transactionIds: ['t_s03', 't_s04'],
    figures: [{ label: { pl: 'Potencjalne oszczędności', en: 'Potential savings' }, amount: '308.99' }],
  },
  {
    match: /weekend/i,
    tool: 'get_category_summary',
    answer: {
      pl: 'W weekendy najwięcej idzie na dostawy jedzenia (8 z 14 zamówień w Glovo) i kawiarnie. Razem to ok. 290 zł w sierpniu — prawie połowa wydatków „na przyjemności”.',
      en: 'At weekends most of your money goes on food delivery (8 of 14 Glovo orders) and cafés — about 290 zł in August, almost half of your fun spending.',
    },
    transactionIds: IDS.glovo.slice(0, 8),
  },
]

export const CHAT_FALLBACK: Record<Language, string> = {
  pl: 'W sierpniu wydatki wyniosły 2 875,40 zł przy wpływach 3 200,00 zł. Do wypłaty możesz bezpiecznie wydać 940,55 zł. O co jeszcze chcesz zapytać — konkretny sklep, kategorię, a może cel?',
  en: 'In August you spent 2,875.40 zł against 3,200.00 zł of income. Until payday you can safely spend 940.55 zł. What else would you like to know — a specific shop, a category, or a goal?',
}
