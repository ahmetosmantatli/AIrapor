import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useUser } from '../context/UserContext'
import './Layout.css'
import './marketing/marketing.css'

const nav = [
  { to: '/app', label: 'Özet', end: true },
  { to: '/app/analysis', label: 'Analiz' },
  { to: '/app/creatives', label: 'Kreatif' },
  { to: '/app/products', label: 'Ürünler' },
  { to: '/app/campaigns', label: 'Kampanyalar' },
  { to: '/app/settings', label: 'Ayarlar' },
  { to: '/app/tools', label: 'Araçlar' },
  { to: '/connect', label: 'Meta' },
]

export function Layout() {
  const { email, logout } = useUser()
  const navigate = useNavigate()

  return (
    <div className="app-shell app-shell--portal">
      <header className="app-header app-header--portal">
        <Link to="/app" className="brand brand--portal">
          <span className="marketing-nav__mark">RA</span>
          <div>
            <strong>Reklam Analiz</strong>
            <p className="brand-sub">Uygulama portalı</p>
          </div>
        </Link>
        <div className="header-actions">
          <Link to="/" className="portal-home-link">
            Ana sayfa
          </Link>
          <span className="muted header-email" title="Oturum">
            {email ?? '—'}
          </span>
          <button
            type="button"
            className="btn ghost small"
            onClick={() => {
              logout()
              navigate('/login', { replace: true })
            }}
          >
            Çıkış
          </button>
        </div>
      </header>

      <nav className="app-nav app-nav--portal" aria-label="Ana menü">
        {nav.map(({ to, label, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end ?? false}
            className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
          >
            {label}
          </NavLink>
        ))}
      </nav>

      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
