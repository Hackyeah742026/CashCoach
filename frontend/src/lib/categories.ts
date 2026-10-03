import type { Category, Language } from '../types'

interface CategoryMeta {
  emoji: string
  /** CSS custom property from index.css */
  color: string
  label: Record<Language, string>
}

export const CATEGORY_META: Record<Category, CategoryMeta> = {
  groceries: { emoji: '🛒', color: 'var(--color-cat-groceries)', label: { pl: 'Zakupy spożywcze', en: 'Groceries' } },
  food_delivery: { emoji: '🛵', color: 'var(--color-cat-food-delivery)', label: { pl: 'Dostawy jedzenia', en: 'Food delivery' } },
  restaurants_cafes: { emoji: '☕', color: 'var(--color-cat-restaurants)', label: { pl: 'Kawiarnie i restauracje', en: 'Cafés & restaurants' } },
  transport: { emoji: '🚌', color: 'var(--color-cat-transport)', label: { pl: 'Transport', en: 'Transport' } },
  rent_bills: { emoji: '🏠', color: 'var(--color-cat-rent-bills)', label: { pl: 'Czynsz i rachunki', en: 'Rent & bills' } },
  subscriptions: { emoji: '🔁', color: 'var(--color-cat-subscriptions)', label: { pl: 'Subskrypcje', en: 'Subscriptions' } },
  shopping: { emoji: '🛍️', color: 'var(--color-cat-shopping)', label: { pl: 'Zakupy', en: 'Shopping' } },
  health_beauty: { emoji: '💊', color: 'var(--color-cat-health)', label: { pl: 'Zdrowie i uroda', en: 'Health & beauty' } },
  entertainment: { emoji: '🎬', color: 'var(--color-cat-entertainment)', label: { pl: 'Rozrywka', en: 'Entertainment' } },
  education: { emoji: '🎓', color: 'var(--color-cat-education)', label: { pl: 'Edukacja', en: 'Education' } },
  travel: { emoji: '✈️', color: 'var(--color-cat-travel)', label: { pl: 'Podróże', en: 'Travel' } },
  transfers_people: { emoji: '👥', color: 'var(--color-cat-other)', label: { pl: 'Przelewy do osób', en: 'Transfers to people' } },
  bnpl: { emoji: '🧾', color: 'var(--color-cat-shopping)', label: { pl: 'Raty / BNPL', en: 'Buy now, pay later' } },
  cash: { emoji: '💵', color: 'var(--color-cat-other)', label: { pl: 'Gotówka', en: 'Cash' } },
  income_salary: { emoji: '💼', color: 'var(--color-cat-groceries)', label: { pl: 'Wynagrodzenie', en: 'Salary' } },
  income_other: { emoji: '💰', color: 'var(--color-cat-groceries)', label: { pl: 'Inne wpływy', en: 'Other income' } },
  savings: { emoji: '🐷', color: 'var(--color-cat-health)', label: { pl: 'Oszczędności', en: 'Savings' } },
  other: { emoji: '📦', color: 'var(--color-cat-other)', label: { pl: 'Inne', en: 'Other' } },
  uncategorized: { emoji: '❔', color: 'var(--color-cat-other)', label: { pl: 'Bez kategorii', en: 'Uncategorized' } },
}

export function categoryLabel(category: Category, lang: Language) {
  return CATEGORY_META[category].label[lang]
}
