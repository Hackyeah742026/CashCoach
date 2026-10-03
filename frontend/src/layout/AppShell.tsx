import { CircleUser, House, MessageCircle, Settings as SettingsIcon, Star, Target, type LucideIcon } from 'lucide-react'
import { NavLink, Outlet } from 'react-router'
import { useChangeLanguage } from '../hooks/useSettings'
import { useLanguage, useT } from '../i18n/context'
import './layout.css'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  end?: boolean
  className?: string
}

export function Brand() {
  return (
    <NavLink to={'/'} className="brand">
      <span className="logo-mark" aria-hidden>
        C
      </span>
      CashCoach
    </NavLink>
  )
}

function LanguageToggle() {
  const t = useT()
  const { lang } = useLanguage()
  const changeLanguage = useChangeLanguage()
  const next = lang === 'pl' ? 'en' : 'pl'
  return (
    <button
      type="button"
      className="lang-toggle"
      onClick={() => changeLanguage(next)}
      aria-label={`${t.common.languageToggle}: ${next.toUpperCase()}`}
    >
      {lang.toUpperCase()}
    </button>
  )
}

/**
 * App frame: phone = top bar + bottom tabs, tablet = icon rail, laptop+ = sidebar.
 * Class names are defined in index.css section 6; NavLink sets aria-current="page".
 */
export function AppShell() {
  const t = useT()

  const items: NavItem[] = [
    { to: '/', label: t.nav.home, icon: House, end: true },
    { to: '/wrapped', label: t.nav.wrapped, icon: Star },
    { to: '/chat', label: t.nav.chat, icon: MessageCircle },
    { to: '/goals', label: t.nav.goals, icon: Target },
    { to: '/settings', label: t.nav.settings, icon: SettingsIcon, className: 'hide-phone' },
  ]

  return (
    <div className="app-shell">
      <a href="#main" className="skip-link">
        {t.common.skipToContent}
      </a>

      <header className="app-bar">
        <Brand />
        <span className="app-bar__spacer" />
        <div className="app-bar__actions">
          <LanguageToggle />
          <NavLink to="/settings" className="icon-btn phone-only" aria-label={t.nav.settings}>
            <CircleUser />
          </NavLink>
        </div>
      </header>

      <nav className="app-nav" aria-label={t.nav.main}>
        <div className="app-nav__brand">
          <Brand />
        </div>
        {items.map(({ to, label, icon: Icon, end, className }) => (
          <NavLink key={to} to={to} end={end} className={`app-nav__item ${className ?? ''}`}>
            <Icon aria-hidden />
            <span className="app-nav__label">{label}</span>
          </NavLink>
        ))}
        <div className="app-nav__footer">
          <div className="app-nav__lang">
            <LanguageToggle />
          </div>
          {t.common.disclaimer}
        </div>
      </nav>

      <main className="app-main" id="main" tabIndex={-1}>
        <div className="container">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
