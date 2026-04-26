import {
  BarChart3,
  Building2,
  Clapperboard,
  ImageIcon,
  Activity,
  LayoutDashboard,
  Link2,
  LogOut,
  Settings,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { getUserProfile, postInsightsRefresh } from '../api/client'
import { useUser } from '../context/UserContext'
import { useAppTheme } from '../theme/appTheme'

const nav: Array<{
  to: string
  label: string
  icon: typeof LayoutDashboard
  end?: boolean
}> = [
  { to: '/app/accounts', label: 'Reklam hesapları', icon: Building2 },
  { to: '/app/dashboard', label: 'Özet', icon: LayoutDashboard },
  { to: '/app/video-report', label: 'AI Video Rapor', icon: Clapperboard },
  { to: '/app/analyzed-ads', label: 'Analiz edilen reklamlar', icon: BarChart3 },
  { to: '/app/impact-tracking', label: 'Etki Takibi', icon: Activity },
  { to: '/app/creatives', label: 'Kreatif', icon: ImageIcon },
  { to: '/app/settings', label: 'Ayarlar', icon: Settings },
  { to: '/connect', label: 'Meta', icon: Link2 },
]

const titleByPath: Record<string, string> = {
  '/app/analysis': 'Analiz',
  '/app/tools': 'Araçlar',
}

export function Layout() {
  const { email, logout, userId } = useUser()
  const { theme, toggleTheme } = useAppTheme()
  const navigate = useNavigate()
  const location = useLocation()
  const [refreshState, setRefreshState] = useState<'ready' | 'loading' | 'success' | 'limit'>('ready')
  const [lastSyncText, setLastSyncText] = useState<string>('—')
  const [activeAct, setActiveAct] = useState<string | null>(null)
  const title =
    titleByPath[location.pathname] ??
    nav.find((n) => (n.end ? location.pathname === n.to : location.pathname.startsWith(n.to)))?.label ??
    'Portal'

  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const p = await getUserProfile(userId)
        if (!cancelled) setActiveAct(p.metaAdAccountId ?? null)
      } catch {
        if (!cancelled) setActiveAct(null)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [userId])

  async function onRefreshClick() {
    setRefreshState('loading')
    try {
      const res = await postInsightsRefresh(userId, activeAct)
      if (res.status === 'limit') {
        setRefreshState('limit')
      } else {
        setRefreshState('success')
      }
      if (res.lastSync) {
        const mins = Math.max(0, Math.round((Date.now() - new Date(res.lastSync).getTime()) / 60000))
        setLastSyncText(`${mins} dk once`)
      }
    } catch {
      setRefreshState('ready')
    } finally {
      window.setTimeout(() => setRefreshState('ready'), 1800)
    }
  }

  return (
    <div className="flex min-h-svh bg-background text-foreground">
      <aside className="flex w-full shrink-0 flex-col border-border bg-card/40 md:w-56 md:border-r">
        <div className="flex items-center gap-2 border-b border-border px-4 py-4">
          <Link to="/app" className="flex items-center gap-2 no-underline">
            <span className="grid size-9 place-items-center rounded-lg bg-primary text-xs font-extrabold text-primary-foreground">
              AD
            </span>
            <div className="leading-tight">
              <div className="text-sm font-semibold tracking-tight">Adlyz</div>
              <div className="text-[11px] text-muted-foreground">Portal</div>
            </div>
          </Link>
        </div>
        <nav className="flex flex-row gap-1 overflow-x-auto px-2 py-3 md:flex-col md:overflow-visible" aria-label="Ana menü">
          {nav.map(({ to, label, end, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={end ?? false}
              className={({ isActive }) =>
                [
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium no-underline transition-colors',
                  isActive
                    ? 'bg-primary/15 text-primary'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                ].join(' ')
              }
            >
              <Icon className="size-4 shrink-0 opacity-80" aria-hidden />
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="mt-auto hidden border-t border-border p-3 md:block">
          <Link
            to="/"
            className="mb-2 block text-xs text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
          >
            Kapak sayfası
          </Link>
        </div>
      </aside>

      <div className="flex min-h-svh min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center justify-between border-b border-border bg-background/80 px-4 backdrop-blur md:px-6">
          <h1 className="text-sm font-medium tracking-tight text-muted-foreground md:text-base">{title}</h1>
          <div className="flex items-center gap-3">
            <div className="hidden sm:block">
              <Button variant="outline" size="sm" onClick={() => void onRefreshClick()} disabled={refreshState === 'loading'}>
                {refreshState === 'loading'
                  ? 'Guncelleniyor...'
                  : refreshState === 'success'
                    ? '✓ Guncellendi'
                    : refreshState === 'limit'
                      ? 'Limit doldu'
                      : '↻ Verileri Guncelle'}
              </Button>
              <div className="mt-1 text-[11px] text-muted-foreground">Son guncelleme: {lastSyncText}</div>
            </div>
            <Button variant="outline" size="sm" onClick={toggleTheme}>
              {theme === 'dark' ? 'Karanlık Mod' : 'Aydınlık Mod'}
            </Button>
            <span className="hidden max-w-[200px] truncate text-xs text-muted-foreground sm:inline" title={email ?? ''}>
              {email ?? '—'}
            </span>
            <Button
              variant="outline"
              size="sm"
              className="gap-1.5"
              onClick={() => {
                logout()
                navigate('/login', { replace: true })
              }}
            >
              <LogOut className="size-3.5" />
              Çıkış
            </Button>
          </div>
        </header>
        <main className="flex-1 overflow-auto p-4 md:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
