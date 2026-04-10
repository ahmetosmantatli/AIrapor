import { useEffect, useState } from 'react'
import { getActiveDirectives, getHealth } from '../api/client'
import type { DirectiveItem, HealthResponse } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

function severityRank(s: string): number {
  if (s === 'critical') return 0
  if (s === 'warning') return 1
  return 2
}

export function Dashboard() {
  const { userId } = useUser()
  const [health, setHealth] = useState<HealthResponse | null>(null)
  const [directives, setDirectives] = useState<DirectiveItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    Promise.all([getHealth(), getActiveDirectives(userId)])
      .then(([h, d]) => {
        if (!cancelled) {
          setHealth(h)
          setDirectives(d)
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) setError(e instanceof Error ? e.message : 'İstek başarısız')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [userId])

  const critical = directives.filter((x) => x.severity === 'critical').length
  const warnings = directives.filter((x) => x.severity === 'warning').length

  const adScores = directives.filter((d) => d.entityType === 'ad' && d.score != null)
  const uniqueAdScores = new Map<string, DirectiveItem>()
  for (const d of adScores.sort((a, b) => b.triggeredAt.localeCompare(a.triggeredAt))) {
    if (!uniqueAdScores.has(d.entityId)) uniqueAdScores.set(d.entityId, d)
  }
  const scores = [...uniqueAdScores.values()].map((d) => d.score as number)
  const avgScore =
    scores.length > 0 ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length) : null

  const topActions = [...directives]
    .sort((a, b) => severityRank(a.severity) - severityRank(b.severity))
    .slice(0, 6)

  return (
    <div className="page">
      <h1 className="page-title">Özet</h1>
      <p className="page-lead">
        API ve direktifler seçili kullanıcı için yüklenir. Oturum açma sonrası burada öncelikli
        aksiyonları görürsünüz.
      </p>

      {loading && <p className="muted">Yükleniyor…</p>}
      {error && <p className="error-banner">{error}</p>}

      {!loading && !error && (
        <>
          <section className="stat-grid" aria-label="Durum kartları">
            <article className="stat-card">
              <h2>API</h2>
              <p className="stat-value">{health?.status ?? '—'}</p>
              <p className="stat-meta">Veritabanı: {health?.database ?? '—'}</p>
            </article>
            <article className="stat-card accent">
              <h2>Ortalama skor</h2>
              <p className="stat-value huge">{avgScore ?? '—'}</p>
              <p className="stat-meta">
                {scores.length > 0 ? `${scores.length} reklam (tahmini)` : 'Reklam skoru yok'}
              </p>
            </article>
            <article className="stat-card warn">
              <h2>Kritik</h2>
              <p className="stat-value">{critical}</p>
              <p className="stat-meta">Direktif</p>
            </article>
            <article className="stat-card">
              <h2>Uyarı</h2>
              <p className="stat-value">{warnings}</p>
              <p className="stat-meta">Direktif</p>
            </article>
          </section>

          <section className="panel">
            <h2 className="panel-title">Öncelikli aksiyonlar</h2>
            {topActions.length === 0 ? (
              <p className="muted">Aktif direktif yok. Önce veri çekip metrik ve direktif üretin.</p>
            ) : (
              <ul className="directive-list">
                {topActions.map((d) => (
                  <li key={d.id} className={`directive-row sev-${d.severity}`}>
                    <span className="tag">{d.entityType}</span>
                    <span className="tag type">{d.directiveType}</span>
                    <p className="directive-msg">{d.message}</p>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  )
}
