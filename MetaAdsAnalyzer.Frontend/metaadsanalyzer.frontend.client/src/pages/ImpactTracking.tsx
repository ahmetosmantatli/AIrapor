import { useEffect, useMemo, useRef, useState } from 'react'
import { getRawInsights } from '../api/client'
import { listAnalyzedAds } from '../features/analyzedAdsStore'
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
  appliedAt: string
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

export function ImpactTracking() {
  const { userId } = useUser()
  const [rows, setRows] = useState<ImpactRow[]>([])
  const [expandedSuggestionId, setExpandedSuggestionId] = useState<number | null>(null)
  const [latestRawRoas, setLatestRawRoas] = useState<number | null>(null)
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
        const reports = await listAnalyzedAds(userId)
        const flat: ImpactRow[] = []
        const reportsByAd = new Map<string, typeof reports>()
        for (const r of reports) {
          const list = reportsByAd.get(r.adId) ?? []
          list.push(r)
          reportsByAd.set(r.adId, list)
        }
        for (const [, list] of reportsByAd) {
          list.sort((a, b) => a.analyzedAt.localeCompare(b.analyzedAt))
        }
        for (const r of reports) {
          for (const s of r.recommendations) {
            const appliedAt = s.appliedAt ?? (s.status === 'applied' ? r.analyzedAt : null)
            if (!appliedAt) continue
            const timeline = (reportsByAd.get(r.adId) ?? [])
              .filter((x) => x.analyzedAt >= appliedAt)
              .map((x) => ({
                at: x.analyzedAt,
                roas: x.aggregate.roas ?? null,
                spend: x.aggregate.spend ?? null,
                purchases: x.aggregate.purchases ?? null,
              }))
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
              beforeSpend: s.beforeSpend ?? null,
              afterSpend: s.afterSpend ?? null,
              beforePurchases: s.beforePurchases ?? null,
              afterPurchases: s.afterPurchases ?? null,
              trend: timeline,
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

  const expandedRow = useMemo(
    () => rows.find((r) => r.suggestionId === expandedSuggestionId) ?? null,
    [rows, expandedSuggestionId],
  )

  const avgRoasDelta = useMemo(() => {
    const measured = rows.filter((r) => r.beforeRoas != null && r.afterRoas != null)
    if (measured.length === 0) return null
    const deltas = measured.map((r) => ((r.afterRoas! - r.beforeRoas!) / Math.abs(r.beforeRoas!)) * 100)
    return deltas.reduce((a, b) => a + b, 0) / deltas.length
  }, [rows])

  const gainDelta = useMemo(() => {
    if (!expandedRow || expandedRow.beforeRoas == null || latestRawRoas == null) return null
    return latestRawRoas - expandedRow.beforeRoas
  }, [expandedRow, latestRawRoas])

  const daysSinceApplied = useMemo(() => {
    if (!expandedRow) return 0
    const d = Math.floor((Date.now() - new Date(expandedRow.appliedAt).getTime()) / 86400000)
    return Math.max(0, d)
  }, [expandedRow])

  const pendingDays = Math.max(0, 7 - daysSinceApplied)

  useEffect(() => {
    let cancelled = false
    if (!expandedRow) {
      setLatestRawRoas(null)
      setLatestRawAt(null)
      return
    }
    ;(async () => {
      try {
        const rows = await getRawInsights(userId, 'ad', { adId: expandedRow.adId, limit: 1 })
        if (cancelled) return
        const r = rows[0]
        setLatestRawRoas(r?.roas ?? null)
        setLatestRawAt(r?.fetchedAt ?? null)
      } catch {
        if (!cancelled) {
          setLatestRawRoas(null)
          setLatestRawAt(null)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [expandedRow, userId])

  useEffect(() => {
    if (!expandedRow || !threeHostRef.current) return
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
  }, [expandedRow])

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
              key={r.suggestionId}
              className="impact-card"
              onClick={() => {
                setExpandedSuggestionId(r.suggestionId)
              }}
            >
              <div className="impact-card-head">
                <div className="analyzed-title">{r.adName}</div>
                <span className="muted small">{days}g</span>
              </div>
              <div className="muted small">Reklam {r.adId}</div>
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
      {expandedRow ? (
        <div
          className="vr-modal-overlay"
          onClick={() => {
            setExpandedSuggestionId(null)
          }}
        >
          <section className="vr-modal impact-modal" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="vr-modal-close"
              onClick={() => {
                setExpandedSuggestionId(null)
              }}
              aria-label="Kapat"
            >
              ×
            </button>
            <h2 className="panel-title">Seçili öneri etkisi</h2>
            <p className="muted small" style={{ marginBottom: '0.75rem' }}>
              Uygulama başlangıcı: {new Date(expandedRow.appliedAt).toLocaleString('tr-TR')} · {expandedRow.adName}
            </p>

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
              {expandedRow.beforeRoas == null ? (
                <p className="impact-banner impact-banner-warn">
                  Meta'da değişiklik yaptıktan sonra 'Uygula' butonuna basın — sistem uygulama öncesi metriklerinizi
                  otomatik kaydeder.
                </p>
              ) : null}
              {expandedRow.beforeRoas != null && latestRawRoas != null && gainDelta != null && Math.abs(gainDelta) < 0.1 && daysSinceApplied >= 7 ? (
                <p className="impact-banner impact-banner-orange">
                  Meta'da değişiklik yapmamış olabilirsiniz — reklamı kontrol edin.
                </p>
              ) : null}
              {daysSinceApplied < 7 ? (
                <p className="impact-banner impact-banner-info">
                  Öneri uygulandı — optimal sonuç için 7 gün beklemeniz önerilir. ({pendingDays} gün kaldı)
                </p>
              ) : null}
              <div className="dashboard-mini-stats">
                <div>
                  <span>Uygulama öncesi ROAS</span>
                  <strong>{expandedRow.beforeRoas == null ? '—' : `${expandedRow.beforeRoas.toFixed(2)}x`}</strong>
                </div>
                <div>
                  <span>Seçilen gün ROAS</span>
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
            </div>
          </section>
        </div>
      ) : null}
    </div>
  )
}

