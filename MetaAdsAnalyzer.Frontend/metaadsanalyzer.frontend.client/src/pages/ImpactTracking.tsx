import { useEffect, useMemo, useState } from 'react'
import { listAnalyzedAds } from '../features/analyzedAdsStore'
import { useUser } from '../context/UserContext'
import './Pages.css'

type ImpactRow = {
  reportId: number
  suggestionId: number
  adName: string
  adId: string
  appliedAt: string
  impactMeasuredAt: string | null
  beforeRoas: number | null
  afterRoas: number | null
  metaChangeDetected: boolean
  metaChangeMessage: string | null
}

export function ImpactTracking() {
  const { userId } = useUser()
  const [rows, setRows] = useState<ImpactRow[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const reports = await listAnalyzedAds(userId)
        const flat: ImpactRow[] = []
        for (const r of reports) {
          for (const s of r.recommendations) {
            const appliedAt = s.appliedAt ?? (s.status === 'applied' ? r.analyzedAt : null)
            if (!appliedAt) continue
            flat.push({
              reportId: Number(r.id) || 0,
              suggestionId: Number(s.id) || 0,
              adName: r.adName ?? r.adId,
              adId: r.adId,
              appliedAt,
              impactMeasuredAt: s.impactMeasuredAt ?? null,
              beforeRoas: s.beforeRoas ?? null,
              afterRoas: s.afterRoas ?? null,
              metaChangeDetected: s.metaChangeDetected !== false,
              metaChangeMessage: s.metaChangeMessage ?? null,
            })
          }
        }
        flat.sort((a, b) => (b.impactMeasuredAt ?? b.appliedAt).localeCompare(a.impactMeasuredAt ?? a.appliedAt))
        if (!cancelled) setRows(flat)
      } catch (e: unknown) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'Etki verisi alınamadı')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [userId])

  const avgRoasDelta = useMemo(() => {
    const measured = rows.filter((r) => r.beforeRoas != null && r.afterRoas != null)
    if (measured.length === 0) return null
    const deltas = measured.map((r) => ((r.afterRoas! - r.beforeRoas!) / Math.abs(r.beforeRoas!)) * 100)
    return deltas.reduce((a, b) => a + b, 0) / deltas.length
  }, [rows])

  return (
    <div className="page">
      <h1 className="page-title">Öneri Etki Takibi</h1>
      <p className="page-lead">
        Uygulanan önerilerin etkisini burada takip edersin.
        {' '}Daha güvenilir sonuçlar için en az 7 gün izleme önerilir.
        {avgRoasDelta != null && <> Ortalama ROAS etkisi: <strong>{avgRoasDelta >= 0 ? '+' : ''}{avgRoasDelta.toFixed(1)}%</strong></>}
      </p>
      {loading && <p className="muted">Yükleniyor…</p>}
      {error && <p className="error-banner">{error}</p>}
      {!loading && !error && rows.length === 0 && <p className="muted">Henüz uygulanmış öneri yok.</p>}
      <div className="analyzed-list">
        {rows.map((r) => {
          const days = Math.floor((Date.now() - new Date(r.appliedAt).getTime()) / 86400000)
          const prog = Math.max(0, Math.min(100, Math.round((days / 7) * 100)))
          const delta = r.beforeRoas != null && r.afterRoas != null
            ? ((r.afterRoas - r.beforeRoas) / Math.abs(r.beforeRoas)) * 100
            : null
          return (
            <div key={r.suggestionId} className="analyzed-row" style={{ cursor: 'default' }}>
              <div className="analyzed-main">
                <div className="analyzed-title">{r.adName}</div>
                <div className="muted small">Reklam {r.adId} · Uygulama: {new Date(r.appliedAt).toLocaleString('tr-TR')}</div>
                {r.impactMeasuredAt ? (
                  <div className={`analyzed-impact ${r.metaChangeDetected ? ((delta ?? 0) >= 0 ? 'analyzed-impact-pos' : 'analyzed-impact-neg') : 'analyzed-impact-wait'}`}>
                    {r.metaChangeDetected ? (
                      <>
                        ROAS: {r.beforeRoas?.toFixed(2) ?? '—'}x → {r.afterRoas?.toFixed(2) ?? '—'}x
                        {delta != null && <> ({delta >= 0 ? '+' : ''}{delta.toFixed(1)}%)</>}
                      </>
                    ) : (
                      <>⚠ Meta Değişimi Tespit Edilmedi · {r.metaChangeMessage}</>
                    )}
                  </div>
                ) : (
                  <div className="analyzed-impact analyzed-impact-wait">
                    Öneri uygulandı — aktif değişim takibi devam ediyor.
                    <div className="analyzed-progress"><span style={{ width: `${prog}%` }} /></div>
                  </div>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

