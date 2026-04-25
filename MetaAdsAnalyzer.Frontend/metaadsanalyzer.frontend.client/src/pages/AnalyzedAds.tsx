import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { listAnalyzedAds, type AnalyzedAdItem, updateRecommendationStatus } from '../features/analyzedAdsStore'
import { getRawInsights } from '../api/client'
import type { RawInsightRow } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

function pct(v: number | null | undefined): string {
  if (v == null || !Number.isFinite(v)) return '—'
  return `${v.toFixed(1)}%`
}

function grade(a: AnalyzedAdItem): { letter: string; breakdown: string[] } {
  const roas = a.aggregate.roas ?? 0
  const hook = a.aggregate.thumbstopPct ?? 0
  const hold = a.aggregate.holdPct ?? 0
  const completion = a.aggregate.completionPct ?? 0
  const letter = roas >= 3 ? 'A' : roas >= 2 ? 'B' : roas >= 1 ? 'C' : 'D'
  return {
    letter,
    breakdown: [
      `ROAS: ${roas.toFixed(1)}x ${roas >= 2 ? '(iyi)' : '(geliştirilmeli)'}`,
      `Hook: ${pct(hook)} ${hook >= 25 ? '(iyi)' : '(zayıf)'}`,
      `Hold: ${pct(hold)} ${hold >= 35 ? '(iyi)' : '(zayıf)'}`,
      `Completion: ${pct(completion)} ${completion >= 15 ? '(iyi)' : '(zayıf)'}`,
    ],
  }
}

type DetailTab = 'funnel' | 'timeline' | 'metrics'
type FunnelStep = { label: string; value: number; lossPct: number | null; lostCount: number }

function buildFunnel(agg: AnalyzedAdItem['aggregate']): FunnelStep[] {
  const isVideo = Boolean(agg.videoP25 || agg.videoP50 || agg.videoP75 || agg.videoP100)
  const steps = isVideo
    ? [
        { label: 'Gösterim', value: agg.impressions },
        { label: '3s İzleme', value: agg.videoPlay3s },
        { label: '%50 İzleme', value: agg.videoP50 },
        { label: 'ThruPlay', value: agg.thruPlay },
        { label: 'Tıklama', value: agg.linkClicks },
        { label: 'Sepet', value: agg.addToCart },
        { label: 'Ödeme Başlat', value: agg.initiateCheckout },
        { label: 'Satın Alma', value: agg.purchases },
      ]
    : [
        { label: 'Gösterim', value: agg.impressions },
        { label: 'Tıklama', value: agg.linkClicks },
        { label: 'Sepet', value: agg.addToCart },
        { label: 'Ödeme Başlat', value: agg.initiateCheckout },
        { label: 'Satın Alma', value: agg.purchases },
      ]
  return steps.map((s, i) => {
    if (i === 0) return { ...s, lossPct: null, lostCount: 0 }
    const prev = steps[i - 1].value
    if (prev <= 0) return { ...s, lossPct: null, lostCount: 0 }
    const lost = Math.max(0, prev - s.value)
    return { ...s, lostCount: lost, lossPct: (lost / prev) * 100 }
  })
}

function statusIcon(ok: boolean, warn: boolean): string {
  if (ok) return '✅'
  if (warn) return '⚠️'
  return '❌'
}

function scoreLabel(score: number | null): { label: string; cls: string } {
  const s = score ?? 0
  if (s >= 80) return { label: 'Winner', cls: 'grade-good' }
  if (s >= 60) return { label: 'Potansiyel', cls: 'grade-mid' }
  if (s >= 40) return { label: 'Zayıf', cls: 'grade-warn' }
  return { label: 'Kapat', cls: 'grade-bad' }
}

function directiveTypeLabel(value: string | null | undefined): string {
  const t = (value ?? '').trim().toUpperCase()
  if (t === 'OPTIMIZE') return 'Optimize Et'
  if (t === 'SCALE') return 'Ölçekle'
  if (t === 'STOP') return 'Durdur'
  if (t === 'WATCH') return 'İzle'
  return value ?? 'İzle'
}

export function AnalyzedAds() {
  const { userId } = useUser()
  const [searchParams] = useSearchParams()
  const [items, setItems] = useState<AnalyzedAdItem[]>([])
  const [active, setActive] = useState<AnalyzedAdItem | null>(null)
  const [detailTab, setDetailTab] = useState<DetailTab>('funnel')
  const [listTab, setListTab] = useState<'pending' | 'completed'>('pending')
  const [activeRawLatest, setActiveRawLatest] = useState<RawInsightRow | null>(null)

  const rows = useMemo(() => items, [items])
  const pendingRows = useMemo(
    () => rows.filter((x) => x.recommendations.every((r) => r.status !== 'applied')),
    [rows],
  )
  const completedRows = useMemo(
    () => rows.filter((x) => x.recommendations.some((r) => r.status === 'applied')),
    [rows],
  )
  const visibleRows = listTab === 'pending' ? pendingRows : completedRows

  useEffect(() => {
    const adId = searchParams.get('adId')
    if (!adId) return
    const item = rows.find((r) => r.adId === adId)
    if (item) setActive(item)
  }, [rows, searchParams])

  function refresh() {
    void listAnalyzedAds(userId).then(setItems).catch(() => setItems([]))
  }

  useEffect(() => {
    refresh()
  }, [userId])

  useEffect(() => {
    let cancelled = false
    if (!active) {
      setActiveRawLatest(null)
      return
    }
    ;(async () => {
      try {
        const rows = await getRawInsights(userId, 'ad')
        if (cancelled) return
        const latest = rows
          .filter((r) => r.entityId === active.adId)
          .sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))[0] ?? null
        setActiveRawLatest(latest)
      } catch {
        if (!cancelled) setActiveRawLatest(null)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [active, userId])

  const activeAggregate = useMemo(() => {
    if (!active) return null
    const base = active.aggregate
    const raw = activeRawLatest
    if (!raw) return base
    return {
      ...base,
      spend: base.spend > 0 ? base.spend : raw.spend,
      impressions: base.impressions > 0 ? base.impressions : raw.impressions,
      linkClicks: base.linkClicks > 0 ? base.linkClicks : raw.linkClicks,
      purchases: base.purchases > 0 ? base.purchases : raw.purchases,
      videoPlay3s: base.videoPlay3s > 0 ? base.videoPlay3s : raw.videoPlay3s,
      videoP100: base.videoP100 > 0 ? base.videoP100 : raw.videoP100,
      roas: base.roas ?? raw.roas ?? null,
      cpa: base.cpa ?? raw.cpa ?? null,
    }
  }, [active, activeRawLatest])

  return (
    <div className="page">
      <h1 className="page-title">Analiz edilen reklamlar</h1>
      <p className="page-lead">Tüm analiz geçmişini burada takip edin. Karta tıklayarak detay raporu açabilirsiniz.</p>
      {rows.length === 0 && <p className="muted">Henüz analiz edilmiş reklam yok.</p>}

      <div className="analyzed-tabs" style={{ marginTop: '0.8rem', marginBottom: '0.55rem' }}>
        <button
          type="button"
          className={`btn ${listTab === 'pending' ? 'primary' : ''}`}
          onClick={() => setListTab('pending')}
        >
          Bekleyen aksiyonlar ({pendingRows.length})
        </button>
        <button
          type="button"
          className={`btn ${listTab === 'completed' ? 'primary' : ''}`}
          onClick={() => setListTab('completed')}
        >
          Tamamlanmış analizler ({completedRows.length})
        </button>
      </div>
      <p className="muted small" style={{ marginBottom: '0.6rem' }}>
        {listTab === 'pending'
          ? 'Henüz "Uygulandı" işaretlenmemiş analizler.'
          : 'En az 1 önerisi uygulanan analizler.'}
      </p>
      <div className="analyzed-list">
        {visibleRows.map((it) => {
          const g = grade(it)
          const appliedCount = it.recommendations.filter((r) => r.status === 'applied').length
          const totalCount = it.recommendations.length
          const impactRef = it.recommendations.find((r) => r.status === 'applied')
          const beforeRoas = impactRef?.beforeRoas ?? null
          const afterRoas = impactRef?.afterRoas ?? null
          const roasDeltaPct = beforeRoas && afterRoas ? ((afterRoas - beforeRoas) / Math.abs(beforeRoas)) * 100 : null
          const daysSinceApplied = impactRef?.appliedAt
            ? Math.floor((Date.now() - new Date(impactRef.appliedAt).getTime()) / 86400000)
            : 0
          const progressPct = Math.max(0, Math.min(100, Math.round((daysSinceApplied / 7) * 100)))
          return (
            <button key={it.id} type="button" className="analyzed-row" onClick={() => setActive(it)}>
              <div className="analyzed-thumb-wrap">
                {it.thumbnailUrl ? <img src={it.thumbnailUrl} alt="" className="analyzed-thumb" /> : <div className="analyzed-thumb-fallback" />}
              </div>
              <div className="analyzed-main">
                <div className="analyzed-title">{it.adName}</div>
                <div className="muted small">
                  {it.campaignName ?? it.campaignId ?? 'kampanya —'} · {it.adsetName ?? it.adsetId ?? 'adset —'} · reklam {it.adId}
                </div>
                <div className="muted small">Analiz: {new Date(it.analyzedAt).toLocaleString('tr-TR')}</div>
                <div className="muted small analyzed-apply-badge">{appliedCount}/{totalCount} öneri uygulandı</div>
                {listTab === 'completed' && impactRef && (
                  <div className={`muted small analyzed-impact ${impactRef.impactMeasuredAt ? ((roasDeltaPct ?? 0) >= 0 ? 'analyzed-impact-pos' : 'analyzed-impact-neg') : 'analyzed-impact-wait'}`}>
                    {impactRef.impactMeasuredAt && impactRef.metaChangeDetected === false ? (
                      <>
                        ⚠ Meta Değişimi Tespit Edilmedi
                        <div>{impactRef.metaChangeMessage ?? 'Öneri uygulandı olarak işaretlendi ancak Meta tarafında anlamlı değişim yok.'}</div>
                      </>
                    ) : impactRef.impactMeasuredAt ? (
                      <>
                        {beforeRoas != null && afterRoas != null
                          ? `ROAS: ${beforeRoas.toFixed(2)}x → ${afterRoas.toFixed(2)}x (${(roasDeltaPct ?? 0) >= 0 ? '+' : ''}${(roasDeltaPct ?? 0).toFixed(1)}%)`
                          : 'Öneri etkisi ölçüldü.'}
                      </>
                    ) : (
                      <>
                        Öneri uygulandı — aktif değişim takibi devam ediyor.
                        <div className="analyzed-progress"><span style={{ width: `${progressPct}%` }} /></div>
                      </>
                    )}
                  </div>
                )}
              </div>
              <div className="analyzed-kpis">
                <div><span className="muted small">ROAS</span><strong>{(it.aggregate.roas ?? 0).toFixed(1)}x</strong></div>
                <div><span className="muted small">Hook</span><strong>{pct(it.aggregate.thumbstopPct)}</strong></div>
                <div><span className="muted small">Hold</span><strong>{pct(it.aggregate.holdPct)}</strong></div>
              </div>
              <div className={`grade-box ${g.letter === 'A' ? 'grade-good' : g.letter === 'B' ? 'grade-mid' : 'grade-bad'}`}>{g.letter}</div>
            </button>
          )
        })}
        {visibleRows.length === 0 && (
          <p className="muted">
            {listTab === 'pending' ? 'Bekleyen aksiyon yok.' : 'Henüz tamamlanmış analiz yok.'}
          </p>
        )}
      </div>

      {active && (
        <div className="vr-modal-overlay" role="dialog" aria-modal="true" aria-label="Detay rapor">
          <div className="vr-modal">
            <button type="button" className="vr-modal-close" onClick={() => setActive(null)} aria-label="Kapat">×</button>
            <div className="vr-modal-main">
              <div className="vr-modal-left">
                {active.thumbnailUrl ? <img src={active.thumbnailUrl} alt="" className="vr-modal-thumb" /> : <div className="vr-modal-thumb-fallback" />}
              </div>
              <div className="vr-modal-right">
                <div className="vr-diagnosis-box">
                  <strong>DERECELENDİRME: {grade(active).letter}</strong>
                  <ul className="muted small" style={{ margin: '0.5rem 0 0', paddingLeft: '1rem' }}>
                    {grade(active).breakdown.map((b) => <li key={b}>{b}</li>)}
                  </ul>
                </div>
                <div className="analyzed-top-meta">
                  <span className={`grade-pill ${scoreLabel(active.aggregate.creativeScore).cls}`}>
                    {scoreLabel(active.aggregate.creativeScore).label}
                  </span>
                  <span className="muted small">Skor: {active.aggregate.creativeScore ?? '—'}/100</span>
                  <span className="muted small">Analiz: {new Date(active.analyzedAt).toLocaleString('tr-TR')}</span>
                  <span className="muted small">Gösterim: {active.aggregate.impressions.toLocaleString('tr-TR')}</span>
                </div>
              </div>
            </div>

            <div className="analyzed-tabs">
              <button type="button" className={`btn ${detailTab === 'funnel' ? 'primary' : ''}`} onClick={() => setDetailTab('funnel')}>Dönüşüm Hunisi</button>
              <button type="button" className={`btn ${detailTab === 'timeline' ? 'primary' : ''}`} onClick={() => setDetailTab('timeline')}>Video Zaman Çizgisi</button>
              <button type="button" className={`btn ${detailTab === 'metrics' ? 'primary' : ''}`} onClick={() => setDetailTab('metrics')}>Metrik Özeti</button>
            </div>

            {detailTab === 'funnel' && (
              <div className="vr-funnel">
                <h3>Dönüşüm Hunisi</h3>
                <div className="vr-funnel-list">
                  {buildFunnel(activeAggregate ?? active.aggregate).map((step, i, all) => {
                    const width = Math.max(28, 100 - i * 8)
                    const highDrop = (step.lossPct ?? 0) > 70
                    return (
                      <div key={`${step.label}-${i}`} className="vr-funnel-item-wrap">
                        <div className={`vr-funnel-item ${highDrop ? 'vr-funnel-item-risk' : ''}`} style={{ width: `${width}%` }}>
                          <span>{step.label}</span>
                          <strong>{step.value.toLocaleString('tr-TR')}</strong>
                          <em>{step.lossPct == null ? '—' : `Düşüş ${step.lossPct.toFixed(1)}%`}</em>
                        </div>
                        {i < all.length - 1 && <span className="vr-funnel-arrow">↓</span>}
                      </div>
                    )
                  })}
                </div>
              </div>
            )}

            {detailTab === 'timeline' && (
              <div className="vr-timeline">
                <h3>Video Zaman Çizgisi</h3>
                <div className="vr-timeline-bar" />
                <div className="vr-timeline-points">
                  {[25, 50, 75, 100].map((pctVal) => {
                    const value =
                      pctVal === 25
                        ? (activeAggregate ?? active.aggregate).videoP25
                        : pctVal === 50
                          ? (activeAggregate ?? active.aggregate).videoP50
                          : pctVal === 75
                            ? (activeAggregate ?? active.aggregate).videoP75
                            : (activeAggregate ?? active.aggregate).videoP100
                    return (
                      <div key={pctVal} className="vr-timeline-point" style={{ left: `${pctVal}%` }}>
                        <span>{pctVal}%</span>
                        <strong>{value.toLocaleString('tr-TR')}</strong>
                      </div>
                    )
                  })}
                </div>
              </div>
            )}

            {detailTab === 'metrics' && (
              <div className="vr-metric-content">
                <h3>Metrik Özeti</h3>
                <table className="data-table compact">
                  <thead>
                    <tr>
                      <th>Metrik Adı</th>
                      <th>Değer</th>
                      <th>Sektör Ortalaması</th>
                      <th>Durum</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr><td>İlk 3 Saniye İzleme (Hook Rate)</td><td>{pct((activeAggregate ?? active.aggregate).thumbstopPct)}</td><td>Ort: %25 · İyi: &gt;%30</td><td>{statusIcon(((activeAggregate ?? active.aggregate).thumbstopPct ?? 0) > 20, ((activeAggregate ?? active.aggregate).thumbstopPct ?? 0) >= 10 && ((activeAggregate ?? active.aggregate).thumbstopPct ?? 0) <= 20)}</td></tr>
                    <tr><td>İçerik Tutma Gücü (Hold Rate)</td><td>{pct((activeAggregate ?? active.aggregate).holdPct)}</td><td>Ort: %20 · İyi: &gt;%25</td><td>{statusIcon(((activeAggregate ?? active.aggregate).holdPct ?? 0) > 15, ((activeAggregate ?? active.aggregate).holdPct ?? 0) >= 8 && ((activeAggregate ?? active.aggregate).holdPct ?? 0) <= 15)}</td></tr>
                    <tr><td>Link Tıklama Oranı (CTR)</td><td>{pct((activeAggregate ?? active.aggregate).ctrLinkPct)}</td><td>Ort: %1.2 · İyi: &gt;%2</td><td>{statusIcon(((activeAggregate ?? active.aggregate).ctrLinkPct ?? 0) > 1, ((activeAggregate ?? active.aggregate).ctrLinkPct ?? 0) >= 0.5 && ((activeAggregate ?? active.aggregate).ctrLinkPct ?? 0) <= 1)}</td></tr>
                    <tr><td>Satın Alma Dönüşümü (CVR)</td><td>{pct((activeAggregate ?? active.aggregate).linkCvrPct)}</td><td>Ort: %1.5 · İyi: &gt;%3</td><td>{statusIcon(((activeAggregate ?? active.aggregate).linkCvrPct ?? 0) > 1, ((activeAggregate ?? active.aggregate).linkCvrPct ?? 0) >= 0.5 && ((activeAggregate ?? active.aggregate).linkCvrPct ?? 0) <= 1)}</td></tr>
                    <tr><td>Yatırım Getirisi (ROAS)</td><td>{((activeAggregate ?? active.aggregate).roas ?? 0).toFixed(2)}x</td><td>Ort: 2.5x · İyi: &gt;4x</td><td>{statusIcon(((activeAggregate ?? active.aggregate).targetRoas ?? 0) > 0 && ((activeAggregate ?? active.aggregate).roas ?? 0) > ((activeAggregate ?? active.aggregate).targetRoas ?? 0), ((activeAggregate ?? active.aggregate).breakEvenRoas ?? 0) > 0 && ((activeAggregate ?? active.aggregate).roas ?? 0) > ((activeAggregate ?? active.aggregate).breakEvenRoas ?? 0))}</td></tr>
                  </tbody>
                </table>
              </div>
            )}

            <div className="vr-recs">
              <h3>Tanılar ve öneriler</h3>
              {active.recommendations.map((r) => {
                const isLocked = r.status !== 'pending'
                return (
                    <div key={r.id} className="vr-rec-row">
                      <span className={`vr-priority ${r.severity === 'critical' ? 'vr-priority-high' : 'vr-priority-mid'}`}>
                        {r.severity === 'critical' ? 'ÖNCELİKLİ' : 'ORTA'}
                      </span>
                      <span className="vr-priority vr-priority-mid">{directiveTypeLabel(r.directiveType)}</span>
                      <p>{r.symptom ?? r.message}</p>
                      <p className="vr-reason">{r.reason ?? 'Neden bilgisi yok.'}</p>
                      <p className="vr-action">{r.action ?? 'İncele ve uygun aksiyonu uygula.'}</p>
                      {r.status !== 'pending' && (
                        <span className={`vr-priority ${r.status === 'applied' ? 'vr-priority-applied' : 'vr-priority-skipped'}`}>
                          {r.status === 'applied' ? 'Uygulandı ✓' : 'Atlandı'}
                        </span>
                      )}
                      <div className="vr-rec-actions">
                        <button
                          type="button"
                          className={`btn ${r.status === 'skipped' ? 'vr-btn-skip' : ''}`}
                          disabled={isLocked}
                          onClick={() => {
                            if (isLocked) return
                            void updateRecommendationStatus(active.id, r.id, 'skipped').then(refresh)
                            setActive((prev) => (prev ? { ...prev, recommendations: prev.recommendations.map((x) => x.id === r.id ? { ...x, status: 'skipped', skippedAt: new Date().toISOString(), appliedAt: null } : x) } : prev))
                          }}
                        >
                          {r.status === 'skipped' ? 'Atlandı' : 'Atla'}
                        </button>
                        <button
                          type="button"
                          className={`btn primary ${r.status === 'applied' ? 'vr-btn-apply' : ''}`}
                          disabled={isLocked}
                          onClick={() => {
                            if (isLocked) return
                            void updateRecommendationStatus(active.id, r.id, 'applied').then(refresh)
                            setActive((prev) => (prev ? { ...prev, recommendations: prev.recommendations.map((x) => x.id === r.id ? { ...x, status: 'applied', appliedAt: new Date().toISOString(), skippedAt: null } : x) } : prev))
                          }}
                        >
                          {r.status === 'applied' ? 'Uygulandı ✓' : 'Uygula'}
                        </button>
                      </div>
                    </div>
                )
              })}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

