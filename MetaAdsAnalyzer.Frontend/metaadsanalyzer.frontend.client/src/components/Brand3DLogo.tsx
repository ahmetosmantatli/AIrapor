import { useEffect, useRef } from 'react'

declare global {
  interface Window {
    THREE?: any
  }
}

const threeCdn = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js'

export function Brand3DLogo({ className = '' }: { className?: string }) {
  const hostRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    let mounted = true
    const host = hostRef.current
    if (!host) return

    let cleanup = () => {}

    const start = () => {
      const THREE = window.THREE
      const hostEl = host
      if (!THREE || !hostEl) return

      const scene = new THREE.Scene()
      const camera = new THREE.PerspectiveCamera(50, hostEl.clientWidth / hostEl.clientHeight, 0.1, 1000)
      camera.position.set(0, 0, 6.4)

      const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true })
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
      renderer.setSize(hostEl.clientWidth, hostEl.clientHeight)
      hostEl.innerHTML = ''
      hostEl.appendChild(renderer.domElement)

      const group = new THREE.Group()
      scene.add(group)

      const shell = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.3, 0),
        new THREE.MeshPhongMaterial({
          color: '#6366f1',
          wireframe: true,
          transparent: true,
          opacity: 0.86,
        }),
      )
      const core = new THREE.Mesh(
        new THREE.OctahedronGeometry(0.72, 0),
        new THREE.MeshPhongMaterial({
          color: '#a78bfa',
          wireframe: true,
          transparent: true,
          opacity: 0.8,
        }),
      )
      const ring = new THREE.Mesh(
        new THREE.TorusGeometry(1.86, 0.04, 12, 80),
        new THREE.MeshPhongMaterial({
          color: '#818cf8',
          transparent: true,
          opacity: 0.65,
        }),
      )
      ring.rotation.x = 1.2
      ring.rotation.y = 0.4
      group.add(shell)
      group.add(core)
      group.add(ring)

      const lightA = new THREE.PointLight('#6366f1', 1.2, 30)
      lightA.position.set(3, 2, 4)
      const lightB = new THREE.PointLight('#22c55e', 0.75, 30)
      lightB.position.set(-2, -1, 3)
      const ambient = new THREE.AmbientLight('#7c83ff', 0.28)
      scene.add(lightA, lightB, ambient)

      let raf = 0
      const startAt = performance.now()
      const animate = (t: number) => {
        const elapsed = (t - startAt) * 0.001
        shell.rotation.y += 0.006
        shell.rotation.x += 0.002
        core.rotation.y -= 0.011
        ring.rotation.z += 0.003
        group.position.y = Math.sin(elapsed * 1.4) * 0.06
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

  return <div ref={hostRef} className={`brand-3d-logo ${className}`.trim()} aria-hidden />
}

