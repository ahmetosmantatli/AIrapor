import { Link, NavLink } from 'react-router-dom'
import { useAuth } from '../../context/UserContext'
import './marketing.css'

type Props = {
  /** Kapak: şeffaf üst; iç sayfalar: beyaz şerit */
  variant?: 'hero' | 'bar'
}

export function MarketingNav({ variant = 'bar' }: Props) {
  const { isAuthenticated } = useAuth()

  return (
    <header className={`marketing-nav marketing-nav--${variant}`}>
      <div className="marketing-nav__inner">
        <Link to="/" className="marketing-nav__logo">
          <span className="marketing-nav__mark">RA</span>
          <span className="marketing-nav__title">Reklam Analiz</span>
        </Link>
        <nav className="marketing-nav__links" aria-label="Üst menü">
          {variant === 'hero' && (
            <a href="#neden" className="marketing-nav__link">
              Neden biz?
            </a>
          )}
          {variant === 'hero' && (
            <a href="#nasil" className="marketing-nav__link">
              Nasıl çalışır?
            </a>
          )}
          {variant === 'hero' && (
            <a href="#fiyat" className="marketing-nav__link">
              Fiyatlandırma
            </a>
          )}
          {isAuthenticated ? (
            <NavLink to="/app" className="marketing-btn marketing-btn--primary marketing-btn--sm">
              Panele git
            </NavLink>
          ) : (
            <>
              <NavLink to="/login" className="marketing-nav__link">
                Giriş yap
              </NavLink>
              <NavLink to="/register" className="marketing-btn marketing-btn--primary marketing-btn--sm">
                Abone ol
              </NavLink>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
