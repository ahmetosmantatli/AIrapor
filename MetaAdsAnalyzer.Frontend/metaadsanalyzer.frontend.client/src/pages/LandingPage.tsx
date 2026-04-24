import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { MarketingNav } from '../components/marketing/MarketingNav'
import './Landing.css'

declare global {
  interface Window {
    THREE?: any
  }
}

const threeCdn = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js'

export function LandingPage() {
  const navigate = useNavigate()
  const canvasHostRef = useRef<HTMLDivElement | null>(null)
  const [theme, setTheme] = useState<'dark' | 'pink'>('dark')

  useEffect(() => {
    let mounted = true
    const host = canvasHostRef.current
    if (!host) return

    let cleanup = () => {}

    function start() {
      const THREE = window.THREE
      if (!THREE || !host) return

      const hostEl = host
      if (!hostEl) return

      const scene = new THREE.Scene()
      const camera = new THREE.PerspectiveCamera(52, hostEl.clientWidth / hostEl.clientHeight, 0.1, 1000)
      camera.position.set(0, 0.2, 8.2)

      const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true })
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
      renderer.setSize(hostEl.clientWidth, hostEl.clientHeight)
      hostEl.innerHTML = ''
      hostEl.appendChild(renderer.domElement)

      const mainGroup = new THREE.Group()
      scene.add(mainGroup)

      const wire = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.55, 0),
        new THREE.MeshPhongMaterial({
          color: '#6366f1',
          wireframe: true,
          transparent: true,
          opacity: 0.85,
        }),
      )
      const fill = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.48, 0),
        new THREE.MeshPhongMaterial({
          color: '#6366f1',
          transparent: true,
          opacity: 0.18,
        }),
      )
      mainGroup.add(wire)
      mainGroup.add(fill)

      const octa = new THREE.Mesh(
        new THREE.OctahedronGeometry(0.9, 0),
        new THREE.MeshPhongMaterial({
          color: '#a78bfa',
          wireframe: true,
          transparent: true,
          opacity: 0.75,
        }),
      )
      mainGroup.add(octa)

      const torusA = new THREE.Mesh(
        new THREE.TorusGeometry(2.25, 0.06, 16, 128),
        new THREE.MeshPhongMaterial({ color: '#6366f1', transparent: true, opacity: 0.75 }),
      )
      torusA.rotation.x = 1.2
      torusA.rotation.y = 0.45
      mainGroup.add(torusA)

      const torusB = new THREE.Mesh(
        new THREE.TorusGeometry(2.65, 0.045, 16, 128),
        new THREE.MeshPhongMaterial({ color: '#a78bfa', transparent: true, opacity: 0.62 }),
      )
      torusB.rotation.x = -0.8
      torusB.rotation.z = 0.5
      mainGroup.add(torusB)

      const aura = new THREE.Mesh(
        new THREE.SphereGeometry(2.15, 28, 28),
        new THREE.MeshPhongMaterial({
          color: '#818cf8',
          transparent: true,
          opacity: 0.08,
          side: THREE.DoubleSide,
        }),
      )
      mainGroup.add(aura)

      const nodeGroup = new THREE.Group()
      const nodePositions: any[] = []
      for (let i = 0; i < 12; i++) {
        const phi = Math.acos(-1 + (2 * i) / 12)
        const theta = Math.sqrt(12 * Math.PI) * phi
        const r = 2.45
        const x = r * Math.cos(theta) * Math.sin(phi)
        const y = r * Math.sin(theta) * Math.sin(phi)
        const z = r * Math.cos(phi)
        nodePositions.push(new THREE.Vector3(x, y, z))
      }
      const nodeColors = ['#22c55e', '#a78bfa', '#6366f1']
      nodePositions.forEach((p, i) => {
        const node = new THREE.Mesh(
          new THREE.SphereGeometry(0.08, 10, 10),
          new THREE.MeshBasicMaterial({ color: nodeColors[i % nodeColors.length] }),
        )
        node.position.copy(p)
        nodeGroup.add(node)
      })
      mainGroup.add(nodeGroup)

      const lineMat = new THREE.LineBasicMaterial({ color: '#818cf8', transparent: true, opacity: 0.45 })
      for (let i = 0; i < nodePositions.length; i++) {
        const j = (i + 3) % nodePositions.length
        const points = [nodePositions[i], nodePositions[j]]
        const geo = new THREE.BufferGeometry().setFromPoints(points)
        const line = new THREE.Line(geo, lineMat)
        mainGroup.add(line)
      }

      const particlesGeo = new THREE.BufferGeometry()
      const particlePos = new Float32Array(200 * 3)
      for (let i = 0; i < 200; i++) {
        particlePos[i * 3] = (Math.random() - 0.5) * 14
        particlePos[i * 3 + 1] = (Math.random() - 0.5) * 9
        particlePos[i * 3 + 2] = (Math.random() - 0.5) * 12
      }
      particlesGeo.setAttribute('position', new THREE.BufferAttribute(particlePos, 3))
      const particles = new THREE.Points(
        particlesGeo,
        new THREE.PointsMaterial({
          color: '#818cf8',
          size: 0.045,
          transparent: true,
          opacity: 0.75,
        }),
      )
      scene.add(particles)

      const lightA = new THREE.PointLight('#6366f1', 1.45, 42)
      lightA.position.set(4, 2, 4)
      const lightB = new THREE.PointLight('#a78bfa', 1.2, 42)
      lightB.position.set(-4, 1.5, 3)
      const lightC = new THREE.PointLight('#22c55e', 0.95, 42)
      lightC.position.set(0, -2, 4)
      const ambient = new THREE.AmbientLight('#6d72ff', 0.32)
      scene.add(lightA)
      scene.add(lightB)
      scene.add(lightC)
      scene.add(ambient)

      const target = { x: 0, y: 0 }
      function onMouseMove(e: MouseEvent) {
        const rect = hostEl.getBoundingClientRect()
        const nx = (e.clientX - rect.left) / rect.width - 0.5
        const ny = (e.clientY - rect.top) / rect.height - 0.5
        target.x = nx * 0.6
        target.y = ny * 0.4
      }
      hostEl.addEventListener('mousemove', onMouseMove)

      let raf = 0
      const startAt = performance.now()
      const animate = (t: number) => {
        const elapsed = (t - startAt) * 0.001
        wire.rotation.y += 0.0045
        wire.rotation.x += 0.0018
        fill.rotation.y += 0.0032
        octa.rotation.y -= 0.009
        octa.rotation.x -= 0.003
        torusA.rotation.z += 0.0025
        torusB.rotation.y -= 0.002
        aura.scale.setScalar(1 + Math.sin(elapsed * 1.7) * 0.03)
        aura.rotation.y += 0.001
        nodeGroup.rotation.y += 0.0015
        particles.rotation.y -= 0.0007
        particles.rotation.x = Math.sin(elapsed * 0.2) * 0.08
        camera.position.z = 8.2 + Math.sin(elapsed * 0.7) * 0.12
        mainGroup.rotation.y += (target.x - mainGroup.rotation.y) * 0.015
        mainGroup.rotation.x += (target.y - mainGroup.rotation.x) * 0.015
        renderer.render(scene, camera)
        raf = requestAnimationFrame(animate)
      }
      raf = requestAnimationFrame(animate)

      const onResize = () => {
        camera.aspect = hostEl.clientWidth / hostEl.clientHeight
        camera.updateProjectionMatrix()
        renderer.setSize(hostEl.clientWidth, hostEl.clientHeight)
      }
      window.addEventListener('resize', onResize)

      cleanup = () => {
        cancelAnimationFrame(raf)
        hostEl.removeEventListener('mousemove', onMouseMove)
        window.removeEventListener('resize', onResize)
        renderer.dispose()
        scene.clear()
      }
    }

    if (window.THREE) {
      start()
    } else {
      const script = document.createElement('script')
      script.src = threeCdn
      script.async = true
      script.onload = () => {
        if (mounted) start()
      }
      document.body.appendChild(script)
      cleanup = () => {
        if (script.parentNode) script.parentNode.removeChild(script)
      }
    }

    return () => {
      mounted = false
      cleanup()
    }
  }, [])

  return (
    <div className={`landing-page landing-page--${theme}`}>
      <MarketingNav variant="hero" />

      <section className="lp-hero">
        <div className="lp-grid-bg" />
        <div className="lp-glow-orb" />
        <div className="landing-hero__inner">
          <button
            type="button"
            className="lp-theme-toggle"
            onClick={() => setTheme((p) => (p === 'dark' ? 'pink' : 'dark'))}
          >
            {theme === 'dark' ? 'Pembe Mod' : 'Koyu Mod'}
          </button>

          <div className="lp-hero-layout">
            <div className="lp-hero-copy">
              <div className="lp-badge">
                <span className="lp-badge-dot" />
                AI destekli Meta reklam analizi
              </div>
              <h1 className="lp-hero-title">
                Meta Reklamlarınızı
                <br />
                AI ile Kâra Dönüştürün
              </h1>
              <p className="lp-hero-lead">
                Video kreatifleri, hook rate, ROAS ve CPA&apos;yı AI ile analiz edin. Ne
                durduracağınızı, ne ölçeklendireceğinizi anında öğrenin.
              </p>
              <div className="lp-hero-cta">
                <Link to="/register" className="marketing-btn marketing-btn--primary marketing-btn--lg">
                  Ücretsiz Analiz Başlat
                </Link>
                <Link to="/login" className="marketing-btn marketing-btn--outline marketing-btn--lg">
                  Demo Gör
                </Link>
              </div>
            </div>

            <div
              className="lp-canvas-box"
              role="button"
              tabIndex={0}
              onClick={() => navigate('/register')}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault()
                  navigate('/register')
                }
              }}
            >
              <div ref={canvasHostRef} className="lp-canvas-host" aria-label="AI 3D görsel sahnesi" />
            </div>
          </div>

        </div>
      </section>

      <section id="neden" className="lp-section">
        <div className="landing-section__inner">
          <div className="lp-highlights-row">
            <div className="lp-highlight-card">
              <strong>Hook Rate %34.2</strong>
              <span className="lp-pill lp-pill--green">+12%</span>
            </div>
            <div className="lp-highlight-card">
              <strong>Kreatif Skor 87/100</strong>
              <span className="lp-pill lp-pill--indigo">Scale et</span>
            </div>
            <div className="lp-highlight-card">
              <strong>DURDUR</strong>
              <span>Hold rate %0 · ROAS 0.6x</span>
            </div>
          </div>
          <h2 className="lp-title">Öne Çıkanlar</h2>
          <div className="lp-bento">
            <article className="lp-card lp-card--wide">
              <div className="lp-card-glow" />
              <h3>AI Video Analizi</h3>
              <p>
                Hook, hold, completion rate otomatik hesaplanır. Videonun hangi saniyesinde izleyici
                kaybettiğinizi görün.
              </p>
              <div className="lp-tags">
                <span>Hook Rate</span>
                <span>Hold Rate</span>
                <span>Thumbstop</span>
                <span>Completion</span>
              </div>
            </article>

            <article className="lp-card">
              <div className="lp-card-glow" />
              <h3>Kreatif Skorlama</h3>
              <p>Her video 0-100 arası skorlanır.</p>
            </article>

            <article className="lp-card">
              <div className="lp-card-glow" />
              <h3>ROAS &amp; CPA Takibi</h3>
              <p>Break-even ROAS anlık hesaplanır.</p>
            </article>

            <article className="lp-card lp-card--wide">
              <div className="lp-card-glow" />
              <h3>Otomatik Direktifler</h3>
              <p>Durdur, ölçeklendir veya optimize et.</p>
              <div className="lp-tags">
                <span className="lp-tag-stop">DURDUR</span>
                <span className="lp-tag-scale">SCALE ET</span>
                <span className="lp-tag-opt">OPTIMIZE ET</span>
              </div>
            </article>
          </div>
        </div>
      </section>

      <section id="nasil" className="lp-section lp-section--alt">
        <div className="landing-section__inner">
          <h2 className="lp-title">Nasıl Çalışır</h2>
          <div className="lp-workflow">
            <div className="lp-step">
              <strong>1. Meta Bağla</strong>
              <p>OAuth ile 30 saniyede</p>
            </div>
            <span className="lp-arrow">→</span>
            <div className="lp-step">
              <strong>2. Video Seç</strong>
              <p>Kampanya ve setten seç</p>
            </div>
            <span className="lp-arrow">→</span>
            <div className="lp-step">
              <strong>3. AI Analiz</strong>
              <p>Metrikler hesaplanır</p>
            </div>
            <span className="lp-arrow">→</span>
            <div className="lp-step">
              <strong>4. Aksiyon Al</strong>
              <p>Direktifi uygula</p>
            </div>
          </div>
        </div>
      </section>

      <section className="lp-section">
        <div className="landing-section__inner">
          <h2 className="lp-title">Sonuçlar</h2>
          <div className="lp-results">
            <article className="lp-result lp-result--bad">
              <h3>Analiz Öncesi</h3>
              <ul>
                <li>ROAS: 0.8x (zarar)</li>
                <li>Hangi video iyi?: Bilinmiyor</li>
                <li>Hook rate: %9 (kritik)</li>
                <li>Bütçe: Boşa akıyor</li>
              </ul>
            </article>
            <article className="lp-result lp-result--good">
              <h3>Analiz Sonrası</h3>
              <ul>
                <li>ROAS: 3.2x (karlı)</li>
                <li>Kazanan video: Tespit edildi</li>
                <li>Hook rate: %34 (iyi)</li>
                <li>Maliyet: %67 düştü</li>
              </ul>
            </article>
          </div>
        </div>
      </section>

      <section id="fiyat" className="lp-section lp-section--alt">
        <div className="landing-section__inner">
          <div className="lp-trust">
            <span>Meta resmi API</span>
            <span>256-bit SSL sifreleme</span>
            <span>Veriler asla satilmaz</span>
          </div>
          <div className="lp-final-cta">
            <h2>Ilk Analizinizi Ucretsiz Yapin</h2>
            <Link to="/register" className="marketing-btn marketing-btn--primary marketing-btn--lg">
              Kayit ol
            </Link>
          </div>
        </div>
      </section>

      <footer className="lp-footer">
        <div className="lp-footer-inner">
          <div className="lp-footer-logo">Reklam Analiz</div>
          <div className="lp-footer-links">
            <Link to="/register">Kayit</Link>
            <Link to="/login">Giris</Link>
            <a href="#neden">Ozellikler</a>
          </div>
        </div>
        <div className="lp-footer-bottom">© 2026 Reklam Analiz</div>
      </footer>
    </div>
  )
}
