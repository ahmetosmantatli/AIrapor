import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getRawInsights, getSavedReportImpactDetail, postInsightsSync } from '../api/client'
import { useUser } from '../context/UserContext'
import type { SavedReportImpactDetail } from '../api/types'
import './Pages.css'

declare global {
  interface Window {
    THREE?: any
  }
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

export function ImpactTrackingDetail() {
  const { userId } = useUser()
  const { suggestionId } = useParams()
  const sid = Number(suggestionId)
  const threeHostRef = useRef<HTMLDivElement | null>(null)
  const logoHostRef = useRef<HTMLDivElement | null>(null)
  const [detail, setDetail] = useState<SavedReportImpactDetail | null>(null)
  const [latestRawRoas, setLatestRawRoas] = useState<number | null>(null)
  const [latestRawSpend, setLatestRawSpend] = useState<number | null>(null)
  const [latestRawPurchases, setLatestRawPurchases] = useState<number | null>(null)
  const [latestRawAt, setLatestRawAt] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    if (!Number.isFinite(sid) || sid <= 0) {
      setError('Geçersiz detay kaydı.')
      setLoading(false)
      return
    }
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const hit = await getSavedReportImpactDetail(sid)
        if (cancelled) return
        setDetail(hit)
      } catch (e: unknown) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'Detay verisi alınamadı')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [sid])

  useEffect(() => {
    let cancelled = false
    if (!detail) return
    ;(async () => {
      try {
        await postInsightsSync(userId, 'ad', 'last_90d', { adId: detail.adId }).catch(() => undefined)
        const rows = await getRawInsights(userId, 'ad', { adId: detail.adId, limit: 1 })
        if (cancelled) return
        const appliedAtMs = new Date(detail.appliedAt).getTime()
        const adRows = rows
          .filter((r) => sameAdId(r.entityId, detail.adId))
          .sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))
        const latestAfterApplied = adRows.find(
          (r) => r.roas != null && Number.isFinite(r.roas) && new Date(r.fetchedAt).getTime() >= appliedAtMs,
        )
        const latestAny = adRows.find((r) => r.roas != null && Number.isFinite(r.roas))
        const selected = latestAfterApplied ?? latestAny ?? null
        setLatestRawRoas(selected?.roas ?? detail.afterRoas ?? detail.beforeRoas ?? null)
        setLatestRawSpend(selected?.spend ?? detail.afterSpend ?? detail.beforeSpend ?? null)
        setLatestRawPurchases(selected?.purchases ?? detail.afterPurchases ?? detail.beforePurchases ?? null)
        setLatestRawAt(selected?.fetchedAt ?? detail.appliedAt ?? null)
      } catch {
        if (!cancelled) {
          setLatestRawRoas(detail.afterRoas ?? detail.beforeRoas ?? null)
          setLatestRawSpend(detail.afterSpend ?? detail.beforeSpend ?? null)
          setLatestRawPurchases(detail.afterPurchases ?? detail.beforePurchases ?? null)
          setLatestRawAt(detail.appliedAt ?? null)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [detail, userId])

  useEffect(() => {
    if (!threeHostRef.current) return
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
      const h = host.clientHeight
      const scene = new THREE.Scene()
      const camera = new THREE.PerspectiveCamera(45, w / h, 0.1, 100)
      camera.position.set(0, 0, 8)
      const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true })
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
      renderer.setSize(w, h)
      host.innerHTML = ''
      host.appendChild(renderer.domElement)
      const mesh = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.8, 1),
        new THREE.MeshPhongMaterial({ color: 0x6366f1, wireframe: true, transparent: true, opacity: 0.45 }),
      )
      scene.add(mesh)
      const ring = new THREE.Mesh(
        new THREE.TorusGeometry(2.8, 0.04, 12, 120),
        new THREE.MeshPhongMaterial({ color: 0xa78bfa, transparent: true, opacity: 0.35 }),
      )
      ring.rotation.x = 1.1
      scene.add(ring)
      scene.add(new THREE.AmbientLight(0x9ca3af, 0.35))
      const light = new THREE.PointLight(0x6366f1, 1.2, 20)
      light.position.set(3, 2, 6)
      scene.add(light)
      const tick = () => {
        if (disposed) return
        mesh.rotation.x += 0.002
        mesh.rotation.y += 0.004
        ring.rotation.z += 0.0015
        renderer.render(scene, camera)
        rafId = requestAnimationFrame(tick)
      }
      tick()
      const onResize = () => {
        const nw = host.clientWidth
        const nh = host.clientHeight
        camera.aspect = nw / nh
        camera.updateProjectionMatrix()
        renderer.setSize(nw, nh)
      }
      window.addEventListener('resize', onResize)
      cleanup = () => {
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
  }, [])

  useEffect(() => {
    if (!logoHostRef.current) return
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
      if (disposed || !logoHostRef.current || !window.THREE) return
      const THREE = window.THREE
      const host = logoHostRef.current
      const w = host.clientWidth
      const h = host.clientHeight
      const scene = new THREE.Scene()
      const camera = new THREE.PerspectiveCamera(48, w / h, 0.1, 1000)
      camera.position.set(0, 0.2, 8.5)
      const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true })
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
      renderer.setSize(w, h)
      host.innerHTML = ''
      host.appendChild(renderer.domElement)

      const group = new THREE.Group()
      scene.add(group)
      const wire = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.9, 0),
        new THREE.MeshPhongMaterial({ color: '#6366f1', wireframe: true, transparent: true, opacity: 0.88 }),
      )
      const fill = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.82, 0),
        new THREE.MeshPhongMaterial({ color: '#6366f1', transparent: true, opacity: 0.16 }),
      )
      const core = new THREE.Mesh(
        new THREE.OctahedronGeometry(1.05, 0),
        new THREE.MeshPhongMaterial({ color: '#a78bfa', wireframe: true, transparent: true, opacity: 0.8 }),
      )
      const torusA = new THREE.Mesh(
        new THREE.TorusGeometry(2.6, 0.06, 16, 128),
        new THREE.MeshPhongMaterial({ color: '#6366f1', transparent: true, opacity: 0.72 }),
      )
      torusA.rotation.x = 1.2
      torusA.rotation.y = 0.35
      const torusB = new THREE.Mesh(
        new THREE.TorusGeometry(3, 0.04, 16, 128),
        new THREE.MeshPhongMaterial({ color: '#a78bfa', transparent: true, opacity: 0.55 }),
      )
      torusB.rotation.x = -0.85
      torusB.rotation.z = 0.45
      group.add(wire, fill, core, torusA, torusB)

      scene.add(new THREE.AmbientLight(0x6d72ff, 0.34))
      const l1 = new THREE.PointLight(0x6366f1, 1.4, 40)
      const l2 = new THREE.PointLight(0xa78bfa, 1.05, 40)
      const l3 = new THREE.PointLight(0x22c55e, 0.8, 40)
      l1.position.set(4, 2, 5)
      l2.position.set(-4, 1.5, 4)
      l3.position.set(0, -2, 4)
      scene.add(l1, l2, l3)

      const startAt = performance.now()
      const tick = (t: number) => {
        if (disposed) return
        const e = (t - startAt) * 0.001
        wire.rotation.y += 0.0048
        wire.rotation.x += 0.0015
        fill.rotation.y += 0.0032
        core.rotation.y -= 0.0078
        torusA.rotation.z += 0.0026
        torusB.rotation.y -= 0.0018
        group.position.y = Math.sin(e * 1.2) * 0.08
        renderer.render(scene, camera)
        rafId = requestAnimationFrame(tick)
      }
      rafId = requestAnimationFrame(tick)

      const onResize = () => {
        const nw = host.clientWidth
        const nh = host.clientHeight
        camera.aspect = nw / nh
        camera.updateProjectionMatrix()
        renderer.setSize(nw, nh)
      }
      window.addEventListener('resize', onResize)
      cleanup = () => {
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
  }, [])

  const daysSinceApplied = useMemo(() => {
    if (!detail) return 0
    return Math.max(0, Math.floor((Date.now() - new Date(detail.appliedAt).getTime()) / 86400000))
  }, [detail])

  const gainDelta = useMemo(() => {
    if (!detail || detail.beforeRoas == null || latestRawRoas == null) return null
    return latestRawRoas - detail.beforeRoas
  }, [detail, latestRawRoas])

  const shouldWarnNoChange = Boolean(
    detail
    && detail.beforeRoas != null
    && latestRawRoas != null
    && gainDelta != null
    && Math.abs(gainDelta) < 0.1
    && daysSinceApplied >= 1,
  )

  const roasDeltaPct = useMemo(() => {
    if (!detail || detail.beforeRoas == null || latestRawRoas == null || detail.beforeRoas === 0) return null
    return ((latestRawRoas - detail.beforeRoas) / Math.abs(detail.beforeRoas)) * 100
  }, [detail, latestRawRoas])

  const spendDelta = useMemo(() => {
    if (!detail || detail.beforeSpend == null || latestRawSpend == null) return null
    return latestRawSpend - detail.beforeSpend
  }, [detail, latestRawSpend])

  const purchasesDelta = useMemo(() => {
    if (!detail || detail.beforePurchases == null || latestRawPurchases == null) return null
    return latestRawPurchases - detail.beforePurchases
  }, [detail, latestRawPurchases])

  return (
    <div className="impact-detail-page">
      <div ref={threeHostRef} className="impact-detail-bg" />
      <div className="impact-detail-logo-wrap">
        <div ref={logoHostRef} className="impact-detail-logo-stage" />
      </div>
      <div className="impact-detail-overlay" />
      <div className="impact-detail-content">
        <div className="impact-detail-head">
          <h1 className="page-title">Adlyz AI Etki Detayı</h1>
          <Link to="/app/impact-tracking" className="btn">Etki Takibine Dön</Link>
        </div>
        {loading && <p className="muted">Yükleniyor…</p>}
        {error && <p className="error-banner">{error}</p>}
        {!loading && !error && detail && (
          <>
            <p className="muted small" style={{ marginBottom: '0.8rem' }}>
              {detail.adName ?? detail.adId} · Uygulama: {new Date(detail.appliedAt).toLocaleString('tr-TR')}
            </p>
            {shouldWarnNoChange ? (
              <p className="impact-banner impact-banner-orange">
                Meta'daki reklam değişikliklerinizi kontrol edin: uygulandı dediğiniz değer kaydedilmemiş olabilir.
              </p>
            ) : null}
            <div className="impact-detail-grid impact-detail-grid-plain">
              <section className="impact-column">
                <h3 className="panel-title">Snapshot (Uygulandığı an)</h3>
                <div className="impact-metric-stream">
                  <div className="impact-metric-row"><span>ROAS</span><strong>{detail.beforeRoas == null ? '—' : `${detail.beforeRoas.toFixed(2)}x`}</strong></div>
                  <div className="impact-metric-row"><span>Harcama</span><strong>{detail.beforeSpend == null ? '—' : `₺${detail.beforeSpend.toFixed(2)}`}</strong></div>
                  <div className="impact-metric-row"><span>Satın Alma</span><strong>{detail.beforePurchases == null ? '—' : detail.beforePurchases}</strong></div>
                </div>
              </section>
              <section className="impact-column">
                <h3 className="panel-title">Mevcut Değerler (Live)</h3>
                <div className="impact-metric-stream">
                  <div className="impact-metric-row"><span>ROAS</span><strong>{latestRawRoas == null ? '—' : `${latestRawRoas.toFixed(2)}x`}</strong></div>
                  <div className="impact-metric-row"><span>Harcama</span><strong>{latestRawSpend == null ? '—' : `₺${latestRawSpend.toFixed(2)}`}</strong></div>
                  <div className="impact-metric-row"><span>Satın Alma</span><strong>{latestRawPurchases == null ? '—' : latestRawPurchases}</strong></div>
                  <div className="impact-metric-row"><span>Son Kontrol</span><strong>{latestRawAt ? new Date(latestRawAt).toLocaleString('tr-TR') : '—'}</strong></div>
                </div>
              </section>
            </div>
            <section className="impact-diff-strip">
              <div><span>ROAS Farkı</span><strong>{gainDelta == null ? '—' : `${gainDelta >= 0 ? '+' : ''}${gainDelta.toFixed(2)}x`}</strong></div>
              <div><span>Fark (%)</span><strong>{roasDeltaPct == null ? '—' : `${roasDeltaPct >= 0 ? '+' : ''}${roasDeltaPct.toFixed(1)}%`}</strong></div>
              <div><span>Harcama Farkı</span><strong>{spendDelta == null ? '—' : `₺${spendDelta >= 0 ? '+' : ''}${spendDelta.toFixed(2)}`}</strong></div>
              <div><span>Satın Alma Farkı</span><strong>{purchasesDelta == null ? '—' : `${purchasesDelta >= 0 ? '+' : ''}${purchasesDelta}`}</strong></div>
              <div><span>Hedef ROAS</span><strong>2.50x</strong></div>
            </section>
            <section className="panel" style={{ marginTop: '0.8rem' }}>
              <h3 className="panel-title">Uygulanan Öneri</h3>
              <p className="muted small" style={{ margin: '0 0 0.4rem' }}>{detail.message ?? 'Öneri metni bulunamadı.'}</p>
              <p className="muted small" style={{ margin: '0 0 0.4rem' }}>{detail.reason ?? 'Neden bilgisi bulunamadı.'}</p>
              <p style={{ margin: 0, fontWeight: 600 }}>{detail.action ?? 'Aksiyon bilgisi bulunamadı.'}</p>
            </section>
          </>
        )}
      </div>
    </div>
  )
}
