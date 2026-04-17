import { useEffect, useState } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { getHealth } from '../../api/client'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useAuth } from '../../context/UserContext'
import './marketing.css'

type Props = {
  variant?: 'hero' | 'bar'
}

type ApiHealth = 'loading' | 'ok' | 'error'

export function MarketingNav({ variant = 'bar' }: Props) {
  const { isAuthenticated } = useAuth()
  const [apiHealth, setApiHealth] = useState<ApiHealth>('loading')

  useEffect(() => {
    let cancelled = false
    getHealth()
      .then(() => {
        if (!cancelled) setApiHealth('ok')
      })
      .catch(() => {
        if (!cancelled) setApiHealth('error')
      })
    return () => {
      cancelled = true
    }
  }, [])

  const apiDown = apiHealth === 'error'
  const apiLoading = apiHealth === 'loading'

  return (
    <header className={`marketing-nav marketing-nav--${variant} flex flex-col`}>
      {apiDown && (
        <div
          className="border-b border-red-900/50 bg-red-950/40 px-4 py-2 text-center text-xs text-red-100"
          role="status"
        >
          API&apos;ye ulaşılamıyor (proxy / sunucu / SQL). Giriş ve kayıt çalışmayabilir.{' '}
          <code className="rounded bg-muted px-1 py-0.5 text-[11px]">/api/health</code> kontrol edin.
        </div>
      )}
      {apiLoading && (
        <div className="border-b border-border bg-muted/30 px-4 py-1.5 text-center text-[11px] text-muted-foreground">
          Bağlantı kontrol ediliyor…
        </div>
      )}
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
            <NavLink
              to="/app"
              className={cn(buttonVariants({ variant: 'default', size: 'sm' }), 'no-underline')}
            >
              Panele git
            </NavLink>
          ) : (
            <>
              {apiDown ? (
                <span className="text-xs text-muted-foreground" title="API kapalı">
                  Giriş (API yok)
                </span>
              ) : (
                <NavLink to="/login" className="marketing-nav__link">
                  Giriş yap
                </NavLink>
              )}
              {apiDown ? (
                <Button size="sm" disabled>
                  Kayıt ol
                </Button>
              ) : (
                <NavLink
                  to="/register"
                  className={cn(buttonVariants({ variant: 'default', size: 'sm' }), 'no-underline')}
                >
                  Kayıt ol
                </NavLink>
              )}
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
