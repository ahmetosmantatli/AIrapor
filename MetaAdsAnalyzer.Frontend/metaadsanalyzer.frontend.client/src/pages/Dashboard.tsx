import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  getActiveDirectives,
  getAuthMe,
  getMetaCampaigns,
  getRawInsights,
  listSavedReportImpacts,
  postInsightsSync,
  getUserProfile,
} from '../api/client'
import type { DirectiveItem, MetaCampaignItem, RawInsightRow, SavedReportImpactFeedItem } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

declare global {
  interface Window {
    Chart?: any
  }
}

export function Dashboard() {
  const navigate = useNavigate()
  const { userId } = useUser()
  const chartRef = useRef<HTMLCanvasElement | null>(null)
  const chartInstanceRef = useRef<any>(null)
  const [name, setName] = useState('Kullanıcı')
  const [lastUpdateText, setLastUpdateText] = useState('—')
  const [dateFilter, setDateFilter] = useState<'14' | '30' | '90'>('30')
  const [refreshTick, setRefreshTick] = useState(0)
  const [campaignId, setCampaignId] = useState<string>('all')
  const [campaigns, setCampaigns] = useState<MetaCampaignItem[]>([])
  const [accountsCount, setAccountsCount] = useState(1)
  const [rawCampaigns, setRawCampaigns] = useState<RawInsightRow[]>([])
  const [rawAdsets, setRawAdsets] = useState<RawInsightRow[]>([])
  const [rawAds, setRawAds] = useState<RawInsightRow[]>([])
  const [directives, setDirectives] = useState<DirectiveItem[]>([])
  const [impactFeed, setImpactFeed] = useState<SavedReportImpactFeedItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const now = new Date()
  const todayLabel = new Intl.DateTimeFormat('tr-TR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    weekday: 'long',
  }).format(now)

  const filteredCampaignRows = useMemo(
    () => (campaignId === 'all' ? rawCampaigns : rawCampaigns.filter((r) => r.metaCampaignId === campaignId)),
    [rawCampaigns, campaignId],
  )

  const filteredAdsetRows = useMemo(
    () => (campaignId === 'all' ? rawAdsets : rawAdsets.filter((r) => r.metaCampaignId === campaignId)),
    [rawAdsets, campaignId],
  )

  const filteredAdRows = useMemo(
    () => (campaignId === 'all' ? rawAds : rawAds.filter((r) => r.metaCampaignId === campaignId)),
    [rawAds, campaignId],
  )

  const latestAdById = useMemo(() => {
    const m = new Map<string, RawInsightRow>()
    for (const row of [...filteredAdRows].sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))) {
      if (!m.has(row.entityId)) m.set(row.entityId, row)
    }
    return m
  }, [filteredAdRows])

  const visibleDirectives = useMemo(() => {
    const list = [...directives].sort((a, b) => b.triggeredAt.localeCompare(a.triggeredAt))
    if (campaignId === 'all') return list.slice(0, 5)
    const adIdSet = new Set([...latestAdById.keys()])
    return list.filter((d) => d.entityType !== 'ad' || adIdSet.has(d.entityId)).slice(0, 5)
  }, [directives, campaignId, latestAdById])

  const directiveTypeLabel = (value: string | null | undefined): string => {
    const t = (value ?? '').trim().toUpperCase()
    if (t === 'OPTIMIZE') return 'Optimize Et'
    if (t === 'SCALE') return 'Ölçekle'
    if (t === 'STOP') return 'Durdur'
    if (t === 'WATCH') return 'İzle'
    return value ?? 'İzle'
  }

  const lastFetchedAt = useMemo(() => {
    const all = [...rawCampaigns, ...rawAdsets, ...rawAds]
    if (all.length === 0) return null
    return all.reduce((max, r) => (r.fetchedAt > max ? r.fetchedAt : max), all[0].fetchedAt)
  }, [rawCampaigns, rawAdsets, rawAds])

  useEffect(() => {
    if (!lastFetchedAt) {
      setLastUpdateText('—')
      return
    }
    const mins = Math.max(0, Math.round((Date.now() - new Date(lastFetchedAt).getTime()) / 60000))
    setLastUpdateText(`${mins} dk önce`)
  }, [lastFetchedAt])

  const dayWindow = Number(dateFilter)
  const syncPreset = useMemo<'last_14d' | 'last_30d' | 'last_90d'>(
    () => (dayWindow >= 90 ? 'last_90d' : dayWindow >= 30 ? 'last_30d' : 'last_14d'),
    [dayWindow],
  )
  const startDate = useMemo(() => {
    const d = new Date()
    d.setHours(0, 0, 0, 0)
    d.setDate(d.getDate() - (dayWindow - 1))
    return d
  }, [dayWindow])

  const inWindowRows = useMemo(() => {
    return filteredCampaignRows.filter((r) => new Date(`${r.dateStart}T00:00:00`).getTime() >= startDate.getTime())
  }, [filteredCampaignRows, startDate])

  const dailyMapWindow = useMemo(() => {
    const map = new Map<string, { ciro: number; spend: number; net: number }>()
    const d = new Date()
    d.setHours(0, 0, 0, 0)
    for (let i = dayWindow - 1; i >= 0; i--) {
      const t = new Date(d)
      t.setDate(d.getDate() - i)
      const key = t.toISOString().slice(0, 10)
      map.set(key, { ciro: 0, spend: 0, net: 0 })
    }
    for (const row of filteredCampaignRows) {
      const key = row.dateStart
      const cur = map.get(key)
      if (!cur) continue
      cur.spend += row.spend
      cur.ciro += row.purchaseValue
      cur.net = cur.ciro - cur.spend
      map.set(key, cur)
    }
    return map
  }, [filteredCampaignRows, dayWindow])

  const kpi = useMemo(() => {
    const byDay = new Map<string, { ciro: number; spend: number }>()
    for (const r of inWindowRows) {
      const key = r.dateStart
      const cur = byDay.get(key) ?? { ciro: 0, spend: 0 }
      cur.ciro += r.purchaseValue
      cur.spend += r.spend
      byDay.set(key, cur)
    }
    const todayKey = new Date().toISOString().slice(0, 10)
    const yKey = new Date(Date.now() - 86400000).toISOString().slice(0, 10)
    const today = byDay.get(todayKey) ?? { ciro: 0, spend: 0 }
    const y = byDay.get(yKey) ?? { ciro: 0, spend: 0 }
    const netToday = today.ciro - today.spend
    const netYesterday = y.ciro - y.spend
    const netDeltaPct = netYesterday !== 0 ? ((netToday - netYesterday) / Math.abs(netYesterday)) * 100 : null
    const last7 = [...byDay.entries()].slice(-7)
    const prev7 = [...byDay.entries()].slice(-14, -7)
    const sum = (arr: Array<[string, { ciro: number; spend: number }]>) =>
      arr.reduce((acc, [, v]) => ({ ciro: acc.ciro + v.ciro, spend: acc.spend + v.spend }), { ciro: 0, spend: 0 })
    const s7 = sum(last7)
    const p7 = sum(prev7)
    const roas7 = s7.spend > 0 ? s7.ciro / s7.spend : 0
    const roasPrev = p7.spend > 0 ? p7.ciro / p7.spend : 0
    const roasDeltaPct = roasPrev > 0 ? ((roas7 - roasPrev) / roasPrev) * 100 : null

    const activeCampaigns = campaigns.filter((c) => (c.status ?? '').toUpperCase() === 'ACTIVE').length
    const adsetCount = new Set(filteredAdsetRows.map((r) => r.entityId)).size
    return {
      netToday,
      netDeltaPct,
      ciroToday: today.ciro,
      spendToday: today.spend,
      roas7,
      roasDeltaPct,
      activeCampaigns,
      adsetCount,
      accountsCount,
      sumWindow: [...dailyMapWindow.values()].reduce(
        (a, v) => ({ ciro: a.ciro + v.ciro, spend: a.spend + v.spend, net: a.net + v.net }),
        { ciro: 0, spend: 0, net: 0 },
      ),
    }
  }, [inWindowRows, campaigns, filteredAdsetRows, dailyMapWindow, accountsCount])

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    ;(async () => {
      try {
        const [me, profile, impacts, d] = await Promise.all([
          getAuthMe(),
          getUserProfile(userId),
          listSavedReportImpacts(userId, 8).catch(() => [] as SavedReportImpactFeedItem[]),
          getActiveDirectives(userId),
        ])
        const email = me.email ?? profile.email ?? ''
        const username = email.split('@')[0]?.trim() || 'Kullanıcı'
        const act = profile.metaAdAccountId ?? undefined
        // Dashboard her açılış/refresh'te güncel snapshot için önce hızlı senkron dener.
        await Promise.allSettled([
          postInsightsSync(userId, 'campaign', syncPreset, { metaAdAccountId: act }),
          postInsightsSync(userId, 'adset', syncPreset, { metaAdAccountId: act }),
          postInsightsSync(userId, 'ad', syncPreset, { metaAdAccountId: act }),
        ])
        const [cs, rc, ra, rad] = await Promise.all([
          getMetaCampaigns(userId, act),
          getRawInsights(userId, 'campaign', campaignId === 'all' ? undefined : { campaignId }),
          getRawInsights(userId, 'adset', campaignId === 'all' ? undefined : { campaignId }),
          getRawInsights(userId, 'ad', campaignId === 'all' ? undefined : { campaignId }),
        ])
        if (!cancelled) {
          setName(username)
          setAccountsCount(Math.max(1, profile.linkedMetaAdAccounts?.length ?? (profile.metaAdAccountId ? 1 : 0)))
          setCampaigns(cs)
          setRawCampaigns(rc)
          setRawAdsets(ra)
          setRawAds(rad)
          setDirectives(d)
          setImpactFeed(impacts)
        }
      } catch (e: unknown) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'İstek başarısız')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [userId, campaignId, refreshTick, syncPreset])

  useEffect(() => {
    const ensureChartScript = async () => {
      if (!window.Chart) {
        await new Promise<void>((resolve, reject) => {
          const script = document.createElement('script')
          script.src = 'https://cdnjs.cloudflare.com/ajax/libs/Chart.js/3.9.1/chart.min.js'
          script.async = true
          script.onload = () => resolve()
          script.onerror = () => reject(new Error('Chart.js yüklenemedi'))
          document.body.appendChild(script)
        })
      }
      const canvas = chartRef.current
      if (!canvas || !window.Chart) return
      const ctx = canvas.getContext('2d')
      if (!ctx) return
      if (chartInstanceRef.current) {
        chartInstanceRef.current.destroy()
      }

      const labels = [...dailyMapWindow.keys()].map((d) => {
        const dt = new Date(`${d}T00:00:00`)
        return `${String(dt.getDate()).padStart(2, '0')}/${String(dt.getMonth() + 1).padStart(2, '0')}`
      })
      const ciro = [...dailyMapWindow.values()].map((x) => x.ciro)
      const net = [...dailyMapWindow.values()].map((x) => x.net)
      const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height)
      gradient.addColorStop(0, 'rgba(129,140,248,0.85)')
      gradient.addColorStop(1, 'rgba(79,70,229,0.35)')

      chartInstanceRef.current = new window.Chart(ctx, {
        type: 'bar',
        data: {
          labels,
          datasets: [
            {
              type: 'bar',
              label: 'Ciro',
              data: ciro,
              backgroundColor: gradient,
              borderRadius: 4,
              yAxisID: 'yCiro',
            },
            {
              type: 'line',
              label: 'Net Kar',
              data: net,
              borderColor: '#10b981',
              borderWidth: 2.5,
              pointBackgroundColor: '#10b981',
              pointBorderColor: '#fff',
              pointBorderWidth: 2,
              pointRadius: 5,
              tension: 0.4,
              fill: false,
              yAxisID: 'yNet',
            },
          ],
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: {
            x: {
              grid: { color: 'rgba(255,255,255,0.04)' },
              ticks: { color: '#a3a3b8' },
            },
            yCiro: {
              position: 'left',
              grid: { color: 'rgba(255,255,255,0.04)' },
              ticks: {
                color: '#a3a3b8',
                callback: (v: number) => `₺${Math.round(v / 1000)}k`,
              },
            },
            yNet: {
              position: 'right',
              grid: { drawOnChartArea: false },
              ticks: {
                color: '#86efac',
                callback: (v: number) => `₺${Math.round(v / 1000)}k`,
              },
            },
          },
        },
      })
    }
    void ensureChartScript()
    return () => {
      if (chartInstanceRef.current) {
        chartInstanceRef.current.destroy()
        chartInstanceRef.current = null
      }
    }
  }, [dailyMapWindow])

  return (
    <div className="page dashboard-v2">
      <div className="dashboard-v2-top">
        <div>
          <h1 className="page-title">İyi günler, {name}</h1>
          <p className="page-lead" style={{ marginBottom: '0.6rem' }}>
            Bugünün özeti · {todayLabel} · Son güncelleme: {lastUpdateText}
          </p>
          <button
            type="button"
            className="btn"
            onClick={() => navigate('/app/impact-tracking')}
            style={{ marginTop: '0.2rem' }}
          >
            Bugünün Güncellemeleri ({impactFeed.length})
          </button>
        </div>
        <div className="form-row">
          <label>
            Tarih filtresi
            <select value={dateFilter} onChange={(e) => setDateFilter(e.target.value as '14' | '30' | '90')}>
              <option value="14">Son 14 gün</option>
              <option value="30">Son 30 gün</option>
              <option value="90">Son 90 gün</option>
            </select>
          </label>
          <label>
            Kampanya
            <select value={campaignId} onChange={(e) => setCampaignId(e.target.value)}>
              <option value="all">Tüm kampanyalar</option>
              {campaigns.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name?.trim() || c.id}
                </option>
              ))}
            </select>
          </label>
          <label>
            &nbsp;
            <button
              type="button"
              className="btn"
              onClick={() => setRefreshTick((x) => x + 1)}
              disabled={loading}
              title="Seçili gün ve kampanyaya göre verileri yeniden getir"
            >
              {loading ? 'Yükleniyor…' : 'Refresh'}
            </button>
          </label>
        </div>
      </div>

      {loading && <p className="muted">Yükleniyor…</p>}
      {error && <p className="error-banner">{error}</p>}

      {!loading && !error && (
        <>
          <section className="stat-grid" aria-label="KPI kartları">
            <article className="stat-card">
              <h2>Bugünkü net kar</h2>
              <p className="stat-value">{`₺${kpi.netToday.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}`}</p>
              <p className="stat-meta">
                {kpi.netDeltaPct == null
                  ? 'Dünkü karşılaştırma yok'
                  : `${kpi.netDeltaPct >= 0 ? '↑' : '↓'} ${Math.abs(kpi.netDeltaPct).toFixed(1)}%`}
              </p>
            </article>
            <article className="stat-card accent">
              <h2>Bugünkü ciro</h2>
              <p className="stat-value huge">{`₺${kpi.ciroToday.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}`}</p>
              <p className="stat-meta">
                Reklam harcaması ₺{kpi.spendToday.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}
              </p>
            </article>
            <article className="stat-card warn">
              <h2>7 günlük ROAS</h2>
              <p className="stat-value">{kpi.roas7.toFixed(2)}x</p>
              <p className="stat-meta">
                {kpi.roasDeltaPct == null
                  ? 'Geçen hafta verisi yok'
                  : `${kpi.roasDeltaPct >= 0 ? '↑' : '↓'} ${Math.abs(kpi.roasDeltaPct).toFixed(1)}%`}
              </p>
            </article>
            <article className="stat-card">
              <h2>Aktif kampanya sayısı</h2>
              <p className="stat-value">{kpi.activeCampaigns}</p>
              <p className="stat-meta">{kpi.accountsCount} reklam hesabı · {kpi.adsetCount} adset</p>
            </article>
          </section>

          <section className="dashboard-v2-grid">
            <article className="panel dashboard-v2-chart">
              <div className="dashboard-v2-head">
                <div>
                  <h2 className="panel-title">Kar / Zarar eğrisi</h2>
                  <p className="muted small">{`MUHASEBE · SON ${dayWindow} GÜN`}</p>
                </div>
                <Link to="/app/analysis" className="muted small">Detayı aç →</Link>
              </div>
              <div className="dashboard-chart-wrap">
                <canvas ref={chartRef} />
              </div>
              <div className="dashboard-mini-stats">
                <div><span>{dayWindow} gün ciro</span><strong>₺{kpi.sumWindow.ciro.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}</strong></div>
                <div><span>{dayWindow} gün reklam</span><strong>₺{kpi.sumWindow.spend.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}</strong></div>
                <div><span>{dayWindow} gün gider</span><strong>₺{kpi.sumWindow.spend.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}</strong></div>
                <div><span>{dayWindow} gün net kar</span><strong className={kpi.sumWindow.net >= 0 ? 'dashboard-pos' : 'dashboard-neg'}>₺{kpi.sumWindow.net.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}</strong></div>
              </div>
            </article>

            <article className="panel">
              <h2 className="panel-title">Bekleyen aksiyonlar</h2>
              <p className="muted small" style={{ marginBottom: '0.65rem' }}>{visibleDirectives.length} öneri onayını bekliyor</p>
              {visibleDirectives.length === 0 ? (
                <p className="muted">Aktif aksiyon önerisi yok.</p>
              ) : (
                <ul className="dashboard-actions-list">
                  {visibleDirectives.map((d) => {
                    const t = (d.directiveType ?? '').toUpperCase()
                    const icon = t === 'STOP' ? '×' : t === 'SCALE' ? '✓' : t === 'OPTIMIZE' ? '!' : 'i'
                    const cls = t === 'STOP' ? 'act-stop' : t === 'SCALE' ? 'act-scale' : t === 'OPTIMIZE' ? 'act-opt' : 'act-watch'
                    const mins = Math.max(0, Math.round((Date.now() - new Date(d.triggeredAt).getTime()) / 60000))
                    return (
                      <li
                        key={d.id}
                        className="dashboard-action-row dashboard-action-clickable"
                        role="button"
                        tabIndex={0}
                        onClick={() => {
                          if (d.entityType === 'ad') {
                            navigate(`/app/analyzed-ads?adId=${encodeURIComponent(d.entityId)}`)
                            return
                          }
                          if (d.entityType === 'adset') {
                            navigate(`/app/analysis?level=adset&adsetId=${encodeURIComponent(d.entityId)}`)
                            return
                          }
                          if (d.entityType === 'campaign') {
                            navigate('/app/campaigns')
                            return
                          }
                          navigate('/app/creatives')
                        }}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault()
                            ;(e.currentTarget as HTMLLIElement).click()
                          }
                        }}
                      >
                        <span className={`dashboard-action-icon ${cls}`}>{icon}</span>
                        <div>
                          <p className="directive-msg"><strong>{directiveTypeLabel(d.directiveType)}:</strong> {d.action ?? d.symptom ?? d.message}</p>
                          <p className="muted small">{mins} dk önce</p>
                        </div>
                        <span className="dashboard-action-arrow">→</span>
                      </li>
                    )
                  })}
                </ul>
              )}
              <Link to="/app/creatives" className="muted small" style={{ marginTop: '0.75rem', display: 'inline-block' }}>
                Tüm direktifleri gör
              </Link>
            </article>
          </section>
        </>
      )}
    </div>
  )
}
