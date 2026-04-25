import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  getActiveDirectives,
  getUserProfile,
  getWatchlist,
  toggleWatchlist,
} from '../api/client'
import type { DirectiveItem } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

export function Creatives() {
  const navigate = useNavigate()
  const { userId } = useUser()
  const [items, setItems] = useState<DirectiveItem[]>([])
  const [watchIds, setWatchIds] = useState<Set<string>>(() => new Set())
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [watchAllowed, setWatchAllowed] = useState(false)

  const reload = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const profile = await getUserProfile(userId)
      const allow = profile.planAllowsWatchlist === true
      setWatchAllowed(allow)
      const d = await getActiveDirectives(userId)
      setItems(d)
      if (allow) {
        const w = await getWatchlist()
        const adSet = new Set(
          w.filter((x) => x.level === 'ad').map((x) => x.entityId),
        )
        setWatchIds(adSet)
      } else {
        setWatchIds(new Set())
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Hata')
    } finally {
      setLoading(false)
    }
  }, [userId])

  useEffect(() => {
    let c = false
    reload().then(() => {
      if (c) return
    })
    return () => {
      c = true
    }
  }, [reload])

  const ads = useMemo(() => {
    const adRows = items.filter((x) => x.entityType === 'ad')
    const byEntity = new Map<string, DirectiveItem>()
    for (const row of adRows.sort((a, b) => b.triggeredAt.localeCompare(a.triggeredAt))) {
      if (!byEntity.has(row.entityId)) byEntity.set(row.entityId, row)
    }
    return [...byEntity.values()].sort((a, b) => (b.score ?? 0) - (a.score ?? 0))
  }, [items])

  const starredAds = useMemo(
    () => ads.filter((x) => watchIds.has(x.entityId)),
    [ads, watchIds],
  )
  const otherAds = useMemo(
    () => ads.filter((x) => !watchIds.has(x.entityId)),
    [ads, watchIds],
  )

  async function onToggleWatch(entityId: string) {
    if (!watchAllowed) return
    setBusyId(entityId)
    setError(null)
    try {
      const r = await toggleWatchlist('ad', entityId)
      setWatchIds((prev) => {
        const next = new Set(prev)
        if (r.isWatching) next.add(entityId)
        else next.delete(entityId)
        return next
      })
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Takip güncellenemedi')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="page">
      <h1 className="page-title">Kreatif skorlar</h1>
      <p className="page-lead">
        Reklam düzeyinde özet skor ve sağlık etiketi (son değerlendirme). Yıldız ile takip listesine
        ekleyin.
      </p>

      {!loading && !watchAllowed && (
        <section className="panel">
          <p className="muted small">
            Takip listesi Pro planda. Ayarlar → Abonelik planı bölümünden Pro’ya geçerek kullanabilirsiniz.
          </p>
        </section>
      )}

      {loading && <p className="muted">Yükleniyor…</p>}
      {error && <p className="error-banner">{error}</p>}

      {!loading && !error && ads.length === 0 && (
        <p className="muted">Reklam direktifi yok. Insights (ad seviyesi) ve direktif üretimini çalıştırın.</p>
      )}

      {watchAllowed && starredAds.length > 0 && (
        <>
          <h2 className="panel-title" style={{ marginTop: '0.75rem' }}>Yıldızlanan reklamlar</h2>
          <p className="muted small" style={{ marginBottom: '0.65rem' }}>
            Yıldız kaldırılmadıkça kalıcıdır. Karta tıklayınca ilgili analiz kartına gidilir.
          </p>
          <ul className="creative-grid">
            {starredAds.map((d) => (
              <li
                key={`star-${d.entityId}`}
                className="creative-card"
                role="button"
                tabIndex={0}
                onClick={() => navigate(`/app/analyzed-ads?adId=${encodeURIComponent(d.entityId)}`)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault()
                    navigate(`/app/analyzed-ads?adId=${encodeURIComponent(d.entityId)}`)
                  }
                }}
              >
                <div className="creative-top">
                  {d.score != null && <span className="score-badge">{d.score}</span>}
                  {d.healthStatus && <span className="health-pill">{d.healthStatus}</span>}
                  <button
                    type="button"
                    className="watch-toggle"
                    title="Takipten çık"
                    disabled={busyId === d.entityId}
                    onClick={(e) => {
                      e.stopPropagation()
                      void onToggleWatch(d.entityId)
                    }}
                    aria-pressed
                  >
                    ★
                  </button>
                </div>
                <p className="creative-id" title={d.entityId}>
                  Reklam ID: {d.entityId}
                </p>
                <p className="creative-msg">{d.message}</p>
              </li>
            ))}
          </ul>
        </>
      )}

      <h2 className="panel-title" style={{ marginTop: '0.75rem' }}>
        {watchAllowed && starredAds.length > 0 ? 'Diğer kreatif skorlar' : 'Tüm kreatif skorlar'}
      </h2>
      <ul className="creative-grid">
        {otherAds.map((d) => (
          <li
            key={d.entityId}
            className="creative-card"
            role="button"
            tabIndex={0}
            onClick={() => navigate(`/app/analyzed-ads?adId=${encodeURIComponent(d.entityId)}`)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                navigate(`/app/analyzed-ads?adId=${encodeURIComponent(d.entityId)}`)
              }
            }}
          >
            <div className="creative-top">
              {d.score != null && <span className="score-badge">{d.score}</span>}
              {d.healthStatus && <span className="health-pill">{d.healthStatus}</span>}
              <button
                type="button"
                className="watch-toggle"
                title={
                  !watchAllowed
                    ? 'Pro plan gerekli'
                    : watchIds.has(d.entityId)
                      ? 'Takipten çık'
                      : 'Takip et'
                }
                disabled={!watchAllowed || busyId === d.entityId}
                onClick={(e) => {
                  e.stopPropagation()
                  void onToggleWatch(d.entityId)
                }}
                aria-pressed={watchIds.has(d.entityId)}
              >
                {watchIds.has(d.entityId) ? '★' : '☆'}
              </button>
            </div>
            <p className="creative-id" title={d.entityId}>
              Reklam ID: {d.entityId}
            </p>
            <p className="creative-msg">{d.message}</p>
          </li>
        ))}
      </ul>
    </div>
  )
}
