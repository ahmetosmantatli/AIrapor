import { useEffect, useMemo, useRef, useState } from 'react'
import { getRawInsights, getSavedReportImpactDetail, listSavedReportImpacts, postInsightsSync } from '../api/client'
import { useUser } from '../context/UserContext'
import './Pages.css'

declare global {
  interface Window {
    THREE?: any
  }
}

type TrendPoint = {
  at: string
  roas: number | null
  spend: number | null
  purchases: number | null
}

type ImpactRow = {
  reportId: number
  suggestionId: number
  adName: string
  adId: string
  campaignName: string | null
  adsetName: string | null
  directiveType: string | null
  severity: string | null
  message: string | null
  reason: string | null
  action: string | null
  appliedAt: string
  analyzedAt: string | null
  impactMeasuredAt: string | null
  beforeRoas: number | null
  afterRoas: number | null
  metaChangeDetected: boolean
  metaChangeMessage: string | null
  beforeSpend: number | null
  afterSpend: number | null
  beforePurchases: number | null
  afterPurchases: number | null
  trend: TrendPoint[]
}

function normalizeEntityId(value: string | null | undefined): string {
  return (value ?? '').trim()
}

function extractDigits(value: string): string {
  return value.replace(/\D+/g, '')
}

function sameAdId(left: string, right: string): boolean {
  const a = normalizeEntityId(left)
  const b = normalizeEntityId(right)
  if (!a || !b) return false
  if (a === b) return true
  const da = extractDigits(a)
  const db = extractDigits(b)
  return da.length >= 6 && db.length >= 6 && da === db
}

export function ImpactTracking() {
  const { userId } = useUser()
  const [rows, setRows] = useState<ImpactRow[]>([])
  const [selectedSuggestionId, setSelectedSuggestionId] = useState<number | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [latestRawRoas, setLatestRawRoas] = useState<number | null>(null)
  const [latestRawSpend, setLatestRawSpend] = useState<number | null>(null)
  const [latestRawPurchases, setLatestRawPurchases] = useState<number | null>(null)
  const [latestRawAt, setLatestRawAt] = useState<string | null>(null)
  const threeHostRef = useRef<HTMLDivElement | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const impacts = await listSavedReportImpacts(userId, 50)
        if (cancelled) return
        const flat: ImpactRow[] = impacts.map((x) => ({
          reportId: x.savedReportId,
          suggestionId: x.suggestionId,
          adName: x.adName ?? x.adId,
          adId: x.adId,
          campaignName: x.campaignName ?? null,
          adsetName: x.adsetName ?? null,
          directiveType: x.directiveType ?? null,
          severity: x.severity ?? null,
          message: x.message ?? null,
          reason: x.reason ?? null,
          action: x.action ?? null,
          appliedAt: x.appliedAt,
          analyzedAt: null,
          impactMeasuredAt: x.impactMeasuredAt ?? null,
          beforeRoas: x.beforeRoas ?? null,
          afterRoas: x.afterRoas ?? null,
          metaChangeDetected: x.metaChangeDetected !== false,
          metaChangeMessage: x.metaChangeMessage ?? null,
          beforeSpend: x.beforeSpend ?? null,
          afterSpend: x.afterSpend ?? null,
          beforePurchases: x.beforePurchases ?? null,
          afterPurchases: x.afterPurchases ?? null,
          trend: [],
        }))
        setRows(flat)
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

  useEffect(() => {
    let cancelled = false
    if (!selectedSuggestionId) return
    ;(async () => {
      try {
        const detail = await getSavedReportImpactDetail(selectedSuggestionId)
        if (cancelled) return
        setRows((prev) =>
          prev.map((r) =>
            r.suggestionId === selectedSuggestionId
              ? {
                  ...r,
                  analyzedAt: detail.analyzedAt,
                  campaignName: detail.campaignName ?? r.campaignName,
                  adsetName: detail.adsetName ?? r.adsetName,
                  directiveType: detail.directiveType ?? r.directiveType,
                  severity: detail.severity ?? r.severity,
                  message: detail.message ?? r.message,
                  reason: detail.reason ?? r.reason,
                  action: detail.action ?? r.action,
                  beforeRoas: detail.beforeRoas ?? r.beforeRoas,
                  afterRoas: detail.afterRoas ?? r.afterRoas,
                  beforeSpend: detail.beforeSpend ?? r.beforeSpend,
                  afterSpend: detail.afterSpend ?? r.afterSpend,
                  beforePurchases: detail.beforePurchases ?? r.beforePurchases,
                  afterPurchases: detail.afterPurchases ?? r.afterPurchases,
                  impactMeasuredAt: detail.impactMeasuredAt ?? r.impactMeasuredAt,
                  metaChangeDetected: detail.metaChangeDetected,
                  metaChangeMessage: detail.metaChangeMessage ?? r.metaChangeMessage,
                }
              : r,
          ),
        )
      } catch {
        // keep feed snapshot if detail fetch fails
      }
    })()
    return () => {
      cancelled = true
    }
  }, [selectedSuggestionId])

  const selectedRow = useMemo(
    () => rows.find((r) => r.suggestionId === selectedSuggestionId) ?? null,
    [rows, selectedSuggestionId],
  )

  const avgRoasDelta = useMemo(() => {
    const measured = rows.filter((r) => r.beforeRoas != null && r.afterRoas != null)
    if (measured.length === 0) return null
    const deltas = measured.map((r) => ((r.afterRoas! - r.beforeRoas!) / Math.abs(r.beforeRoas!)) * 100)
    return deltas.reduce((a, b) => a + b, 0) / deltas.length
  }, [rows])

  const gainDelta = useMemo(() => {
    if (!selectedRow || selectedRow.beforeRoas == null || latestRawRoas == null) return null
    return latestRawRoas - selectedRow.beforeRoas
  }, [selectedRow, latestRawRoas])

  const daysSinceApplied = useMemo(() => {
    if (!selectedRow) return 0
    const d = Math.floor((Date.now() - new Date(selectedRow.appliedAt).getTime()) / 86400000)
    return Math.max(0, d)
  }, [selectedRow])

  const pendingDays = Math.max(0, 7 - daysSinceApplied)

  useEffect(() => {
    let cancelled = false
    if (!selectedRow) {
      setLatestRawRoas(null)
      setLatestRawSpend(null)
      setLatestRawPurchases(null)
      setLatestRawAt(null)
      return
    }
    ;(async () => {
      try {
        // Kart açıldığında ilgili reklam için güncel insight çekmeyi dener.
        await postInsightsSync(userId, 'ad', 'last_90d', { adId: selectedRow.adId }).catch(() => undefined)
        const rows = await getRawInsights(userId, 'ad', { adId: selectedRow.adId, limit: 1 })
        if (cancelled) return
        const appliedAtMs = new Date(selectedRow.appliedAt).getTime()
        const adRows = rows
          .filter((r) => sameAdId(r.entityId, selectedRow.adId))
          .sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))
        const latestAfterApplied = adRows.find(
          (r) => r.roas != null && Number.isFinite(r.roas) && new Date(r.fetchedAt).getTime() >= appliedAtMs,
        )
        const latestAny = adRows.find((r) => r.roas != null && Number.isFinite(r.roas))
        const selected = latestAfterApplied ?? latestAny ?? null
        setLatestRawRoas(
          selected?.roas ?? selectedRow.afterRoas ?? selectedRow.beforeRoas ?? null,
        )
        setLatestRawSpend(selected?.spend ?? selectedRow.afterSpend ?? selectedRow.beforeSpend ?? null)
        setLatestRawPurchases(selected?.purchases ?? selectedRow.afterPurchases ?? selectedRow.beforePurchases ?? null)
        setLatestRawAt(selected?.fetchedAt ?? selectedRow.appliedAt ?? null)
      } catch {
        if (!cancelled) {
          setLatestRawRoas(selectedRow.afterRoas ?? selectedRow.beforeRoas ?? null)
          setLatestRawSpend(selectedRow.afterSpend ?? selectedRow.beforeSpend ?? null)
          setLatestRawPurchases(selectedRow.afterPurchases ?? selectedRow.beforePurchases ?? null)
          setLatestRawAt(selectedRow.appliedAt ?? null)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [selectedRow, userId])

  useEffect(() => {
    if (!detailOpen || !selectedRow || !threeHostRef.current) return
    let disposed = false
    let rafId = 0
    let cleanup: (() => void) | null = null

    const init = async () => {
      if (!window.THREE) {
        await new Promise<void>((resolve, reject) => {
          const existing = document.querySelector('script[data-three-r128="1"]') as HTMLScriptElement | null
          if (existing) {
            existing.addEventListener('load', () => resolve(), { once: true })
            existing.addEventListener('error', () => reject(new Error('three.js yüklenemedi')), { once: true })
            return
          }
          const script = document.createElement('script')
          script.src = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js'
          script.async = true
          script.dataset.threeR128 = '1'
          script.onload = () => resolve()
          script.onerror = () => reject(new Error('three.js yüklenemedi'))
          document.body.appendChild(script)
        })
      }
      if (disposed || !threeHostRef.current || !window.THREE) return
      const THREE = window.THREE
      const host = threeHostRef.current
      const w = host.clientWidth
      const h = 220

      const scene = new THREE.Scene()
      scene.background = new THREE.Color('#0a0a0f')
      const camera = new THREE.PerspectiveCamera(45, w / h, 0.1, 100)
      camera.position.set(0, 0, 8)

      const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false })
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
      renderer.setSize(w, h)
      host.innerHTML = ''
      host.appendChild(renderer.domElement)

      const main = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.25, 1),
        new THREE.MeshPhongMaterial({
          color: 0x6366f1,
          wireframe: true,
          transparent: true,
          opacity: 0.55,
        }),
      )
      scene.add(main)

      const inner = new THREE.Mesh(
        new THREE.OctahedronGeometry(0.68, 0),
        new THREE.MeshPhongMaterial({ color: 0xa78bfa, transparent: true, opacity: 0.75 }),
      )
      scene.add(inner)

      const ring1 = new THREE.Mesh(
        new THREE.TorusGeometry(1.9, 0.08, 16, 100),
        new THREE.MeshPhongMaterial({ color: 0x4f46e5, transparent: true, opacity: 0.75 }),
      )
      ring1.rotation.x = 0.95
      ring1.rotation.y = 0.4
      scene.add(ring1)

      const ring2 = new THREE.Mesh(
        new THREE.TorusGeometry(2.35, 0.03, 14, 120),
        new THREE.MeshPhongMaterial({ color: 0xe5e7eb, transparent: true, opacity: 0.36 }),
      )
      ring2.rotation.x = 1.15
      ring2.rotation.y = 0.2
      scene.add(ring2)

      const ring3 = new THREE.Mesh(
        new THREE.TorusGeometry(1.2, 0.02, 12, 80),
        new THREE.MeshPhongMaterial({ color: 0xdbeafe, transparent: true, opacity: 0.4 }),
      )
      ring3.rotation.y = Math.PI / 2
      ring3.rotation.z = 0.25
      scene.add(ring3)

      const nodeGroup = new THREE.Group()
      const nodePos: number[] = []
      for (let i = 0; i < 8; i++) {
        const a = (i / 8) * Math.PI * 2
        const r = 2.8
        const x = Math.cos(a) * r
        const y = Math.sin(a) * 1.2
        const z = Math.sin(a) * r * 0.4
        nodePos.push(x, y, z)
        const node = new THREE.Mesh(
          new THREE.SphereGeometry(0.09, 12, 12),
          new THREE.MeshBasicMaterial({ color: i % 2 === 0 ? 0x22c55e : 0xa78bfa }),
        )
        node.position.set(x, y, z)
        nodeGroup.add(node)
      }
      scene.add(nodeGroup)
      const links = new THREE.BufferGeometry()
      links.setAttribute('position', new THREE.Float32BufferAttribute(nodePos, 3))
      const line = new THREE.LineLoop(links, new THREE.LineBasicMaterial({ color: 0x7c83ff, transparent: true, opacity: 0.4 }))
      scene.add(line)

      const particleGeo = new THREE.BufferGeometry()
      const particlePos = new Float32Array(120 * 3)
      for (let i = 0; i < 120; i++) {
        particlePos[i * 3] = (Math.random() - 0.5) * 10
        particlePos[i * 3 + 1] = (Math.random() - 0.5) * 6
        particlePos[i * 3 + 2] = (Math.random() - 0.5) * 8
      }
      particleGeo.setAttribute('position', new THREE.BufferAttribute(particlePos, 3))
      const particles = new THREE.Points(
        particleGeo,
        new THREE.PointsMaterial({ color: 0x93c5fd, size: 0.035, transparent: true, opacity: 0.65 }),
      )
      scene.add(particles)

      const l1 = new THREE.PointLight(0x6366f1, 1.2, 22)
      l1.position.set(4, 2, 5)
      const l2 = new THREE.PointLight(0xa78bfa, 1.05, 22)
      l2.position.set(-4, -1.5, 4)
      const l3 = new THREE.PointLight(0x22c55e, 0.9, 22)
      l3.position.set(0, 3, -3)
      scene.add(l1, l2, l3)
      scene.add(new THREE.AmbientLight(0x9ca3af, 0.28))

      let mx = 0
      let my = 0
      const onMouseMove = (e: MouseEvent) => {
        const rect = host.getBoundingClientRect()
        mx = ((e.clientX - rect.left) / rect.width - 0.5) * 0.9
        my = ((e.clientY - rect.top) / rect.height - 0.5) * 0.9
      }
      host.addEventListener('mousemove', onMouseMove)

      const onResize = () => {
        const nw = host.clientWidth
        camera.aspect = nw / h
        camera.updateProjectionMatrix()
        renderer.setSize(nw, h)
      }
      window.addEventListener('resize', onResize)

      const tick = () => {
        if (disposed) return
        main.rotation.x += 0.004
        main.rotation.y += 0.006
        inner.rotation.x -= 0.007
        inner.rotation.y -= 0.005
        ring1.rotation.z += 0.003
        ring2.rotation.z -= 0.0024
        ring3.rotation.x += 0.0045
        nodeGroup.rotation.y += 0.002
        line.rotation.y += 0.002
        particles.rotation.y += 0.0009
        scene.rotation.y += (mx - scene.rotation.y) * 0.035
        scene.rotation.x += (my - scene.rotation.x) * 0.025
        renderer.render(scene, camera)
        rafId = requestAnimationFrame(tick)
      }
      tick()

      cleanup = () => {
        host.removeEventListener('mousemove', onMouseMove)
        window.removeEventListener('resize', onResize)
        cancelAnimationFrame(rafId)
        renderer.dispose()
        host.innerHTML = ''
      }
    }

    void init()
    return () => {
      disposed = true
      cleanup?.()
    }
  }, [detailOpen, selectedRow])

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
      <div className="impact-grid">
        {rows.map((r) => {
          const days = Math.floor((Date.now() - new Date(r.appliedAt).getTime()) / 86400000)
          const prog = Math.max(0, Math.min(100, Math.round((days / 7) * 100)))
          const delta = r.beforeRoas != null && r.afterRoas != null
            ? ((r.afterRoas - r.beforeRoas) / Math.abs(r.beforeRoas)) * 100
            : null
          return (
            <div
              key={`${r.reportId}-${r.suggestionId}-${r.appliedAt}`}
              className="impact-card"
              onClick={() => {
                setSelectedSuggestionId(r.suggestionId)
                setDetailOpen(false)
              }}
            >
              <div className="impact-card-head">
                <div className="analyzed-title">{r.adName}</div>
                <span className="muted small">{days}g</span>
              </div>
              <div className="muted small">Reklam {r.adId}</div>
              <div className="muted small">{r.campaignName ?? 'Kampanya —'} · {r.adsetName ?? 'Adset —'}</div>
              <div className="muted small">Uygulama: {new Date(r.appliedAt).toLocaleString('tr-TR')}</div>
              <div className="impact-card-body">
                {r.impactMeasuredAt ? (
                  <div className={`analyzed-impact ${r.metaChangeDetected ? ((delta ?? 0) >= 0 ? 'analyzed-impact-pos' : 'analyzed-impact-neg') : 'analyzed-impact-wait'}`}>
                    {r.metaChangeDetected ? (
                      <>
                        ROAS: {r.beforeRoas?.toFixed(2) ?? '—'}x → {r.afterRoas?.toFixed(2) ?? '—'}x{' '}
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

      {selectedRow ? (
        <div
          className="vr-modal-overlay"
          onClick={() => {
            setSelectedSuggestionId(null)
            setDetailOpen(false)
          }}
        >
          <section className="vr-modal impact-modal" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="vr-modal-close"
              onClick={() => {
                setSelectedSuggestionId(null)
                setDetailOpen(false)
              }}
              aria-label="Kapat"
            >
              ×
            </button>
            <h2 className="panel-title">Seçili öneri etkisi</h2>
            <p className="muted small" style={{ marginBottom: '0.75rem' }}>
              Uygulama başlangıcı: {new Date(selectedRow.appliedAt).toLocaleString('tr-TR')} · {selectedRow.adName}
            </p>
            <div className="dashboard-mini-stats" style={{ marginBottom: '0.75rem' }}>
              <div>
                <span>Öneri Tipi</span>
                <strong>{selectedRow.directiveType ?? '—'}</strong>
              </div>
              <div>
                <span>Öncelik</span>
                <strong>{selectedRow.severity ?? '—'}</strong>
              </div>
              <div>
                <span>Kampanya / Adset</span>
                <strong>{selectedRow.campaignName ?? 'Kampanya —'} · {selectedRow.adsetName ?? 'Adset —'}</strong>
              </div>
              <div>
                <span>İzlenen Durum</span>
                <strong>{selectedRow.metaChangeDetected ? 'Aktif değişim takibi' : 'Meta değişimi tespit edilmedi'}</strong>
              </div>
            </div>
            <div className="impact-banner impact-banner-info" style={{ marginBottom: 0 }}>
              Detaylı before/after kıyas için Detaylar butonuna tıklayın.
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '0.75rem' }}>
              <button
                type="button"
                className="btn primary"
                onClick={() => {
                  setDetailOpen(true)
                }}
              >
                Detaylar
              </button>
            </div>
          </section>
        </div>
      ) : null}

      {detailOpen && selectedRow ? (
        <div
          className="vr-modal-overlay"
          onClick={() => {
            setDetailOpen(false)
          }}
        >
          <section className="vr-modal impact-modal" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="vr-modal-close"
              onClick={() => {
                setDetailOpen(false)
              }}
              aria-label="Kapat"
            >
              ×
            </button>
            <h2 className="panel-title">Seçili öneri etkisi</h2>
            <p className="muted small" style={{ marginBottom: '0.75rem' }}>
              Uygulama başlangıcı: {new Date(selectedRow.appliedAt).toLocaleString('tr-TR')} · {selectedRow.adName}
            </p>
            <div className="dashboard-mini-stats" style={{ marginBottom: '0.75rem' }}>
              <div>
                <span>Öneri Tipi</span>
                <strong>{selectedRow.directiveType ?? '—'}</strong>
              </div>
              <div>
                <span>Öncelik</span>
                <strong>{selectedRow.severity ?? '—'}</strong>
              </div>
              <div>
                <span>Mesaj</span>
                <strong>{selectedRow.message ?? '—'}</strong>
              </div>
              <div>
                <span>Aksiyon</span>
                <strong>{selectedRow.action ?? '—'}</strong>
              </div>
            </div>

            <div className="impact-modal-chart">
              <h3 className="panel-title impact-modal-subtitle">AI görselleştirme</h3>
              <div
                ref={threeHostRef}
                style={{
                  width: '100%',
                  height: '220px',
                  background: '#0a0a0f',
                  borderRadius: '10px',
                  border: '1px solid rgba(255,255,255,0.08)',
                  overflow: 'hidden',
                }}
              />
            </div>

            <div>
              <h3 className="panel-title impact-modal-subtitle">Seçilen gün performans farkı</h3>
              {selectedRow.beforeRoas == null ? (
                <p className="impact-banner impact-banner-warn">
                  Meta'da değişiklik yaptıktan sonra 'Uygula' butonuna basın — sistem uygulama öncesi metriklerinizi
                  otomatik kaydeder.
                </p>
              ) : null}
              {selectedRow.beforeRoas != null && latestRawRoas != null && gainDelta != null && Math.abs(gainDelta) < 0.1 && daysSinceApplied >= 1 ? (
                <p className="impact-banner impact-banner-orange">
                  Meta reklam hesabınızı kontrol ediniz: Uygulandı kabul ettiğiniz değerler uygulanmamıştır.
                </p>
              ) : null}
              {daysSinceApplied < 7 ? (
                <p className="impact-banner impact-banner-info">
                  Öneri uygulandı — optimal sonuç için 7 gün beklemeniz önerilir. ({pendingDays} gün kaldı)
                </p>
              ) : null}
              <div className="dashboard-mini-stats">
                <div>
                  <span>Uygulama öncesi ROAS <em className="impact-source impact-source-snapshot">Snapshot</em></span>
                  <strong>{selectedRow.beforeRoas == null ? '—' : `${selectedRow.beforeRoas.toFixed(2)}x`}</strong>
                </div>
                <div>
                  <span>Seçilen gün ROAS <em className="impact-source impact-source-live">Live</em></span>
                  <strong>{latestRawRoas == null ? '—' : `${latestRawRoas.toFixed(2)}x`}</strong>
                </div>
                <div>
                  <span>Kazanç farkı</span>
                  <strong className={gainDelta != null && gainDelta >= 0 ? 'dashboard-pos' : 'dashboard-neg'}>
                    {gainDelta == null ? '—' : `${gainDelta >= 0 ? '+' : ''}${gainDelta.toFixed(1)}x`}
                  </strong>
                </div>
                <div>
                  <span>İncelenen tarih</span>
                  <strong>{latestRawAt ? new Date(latestRawAt).toLocaleString('tr-TR') : '—'}</strong>
                </div>
              </div>
              <div className="dashboard-mini-stats" style={{ marginTop: '0.5rem' }}>
                <div>
                  <span>Uygulama öncesi Harcama <em className="impact-source impact-source-snapshot">Snapshot</em></span>
                  <strong>{selectedRow.beforeSpend == null ? '—' : `₺${selectedRow.beforeSpend.toFixed(2)}`}</strong>
                </div>
                <div>
                  <span>Güncel Harcama <em className="impact-source impact-source-live">Live</em></span>
                  <strong>{latestRawSpend == null ? '—' : `₺${latestRawSpend.toFixed(2)}`}</strong>
                </div>
                <div>
                  <span>Uygulama öncesi Satın Alma <em className="impact-source impact-source-snapshot">Snapshot</em></span>
                  <strong>{selectedRow.beforePurchases == null ? '—' : selectedRow.beforePurchases}</strong>
                </div>
                <div>
                  <span>Güncel Satın Alma <em className="impact-source impact-source-live">Live</em></span>
                  <strong>{latestRawPurchases == null ? '—' : latestRawPurchases}</strong>
                </div>
              </div>

              <div className="panel" style={{ marginTop: '0.75rem', marginBottom: 0 }}>
                <h3 className="panel-title" style={{ marginBottom: '0.4rem' }}>Uygulanan öneri</h3>
                <p className="muted small" style={{ margin: 0 }}>
                  {selectedRow.message ?? 'Öneri metni bulunamadı.'}
                </p>
                <p className="muted small" style={{ margin: '0.35rem 0 0' }}>
                  {selectedRow.reason ?? 'Neden bilgisi bulunamadı.'}
                </p>
                <p style={{ margin: '0.4rem 0 0', fontWeight: 600 }}>
                  {selectedRow.action ?? 'Aksiyon bilgisi bulunamadı.'}
                </p>
              </div>
            </div>
          </section>
        </div>
      ) : null}
    </div>
  )
}

