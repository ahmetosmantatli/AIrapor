import { useEffect, useState } from 'react'
import {
  downloadAnalysisPdf,
  getUserProfile,
  postDirectivesEvaluate,
  postInsightsSync,
  postMetricsRecompute,
} from '../api/client'
import { useUser } from '../context/UserContext'
import './Pages.css'

export function Tools() {
  const { userId } = useUser()
  const [level, setLevel] = useState('campaign')
  const [preset, setPreset] = useState('last_7d')
  const [log, setLog] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [pdfAllowed, setPdfAllowed] = useState<boolean | null>(null)

  useEffect(() => {
    let c = false
    getUserProfile(userId)
      .then((p) => {
        if (!c) setPdfAllowed(p.planAllowsPdfExport === true)
      })
      .catch(() => {
        if (!c) setPdfAllowed(false)
      })
    return () => {
      c = true
    }
  }, [userId])

  async function run(label: string, fn: () => Promise<unknown>) {
    setBusy(true)
    setLog(null)
    try {
      const r = await fn()
      setLog(`${label}: ${JSON.stringify(r)}`)
    } catch (e: unknown) {
      setLog(`${label} hata: ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="page">
      <h1 className="page-title">Veri ve hesap</h1>
      <p className="page-lead">
        Geliştirme sırasında arka uç adımlarını buradan tetikleyebilirsiniz. Üretimde bunlar zamanlama veya
        yönetim panelinden çalışır.
      </p>

      <section className="panel">
        <h2 className="panel-title">Meta insights senkron</h2>
        <div className="form-row">
          <label>
            Seviye
            <select value={level} onChange={(e) => setLevel(e.target.value)}>
              <option value="campaign">campaign</option>
              <option value="adset">adset</option>
              <option value="ad">ad</option>
            </select>
          </label>
          <label>
            Tarih preset
            <input value={preset} onChange={(e) => setPreset(e.target.value)} />
          </label>
        </div>
        <button
          type="button"
          className="btn primary"
          disabled={busy}
          onClick={() =>
            run('Insights sync', () => postInsightsSync(userId, level, preset))
          }
        >
          Insights çek
        </button>
      </section>

      <section className="panel">
        <h2 className="panel-title">Hesaplanmış metrikler</h2>
        <button
          type="button"
          className="btn"
          disabled={busy}
          onClick={() => run('Metrik recompute', () => postMetricsRecompute(userId))}
        >
          Tüm raw için yeniden hesapla
        </button>
      </section>

      <section className="panel">
        <h2 className="panel-title">Direktifler</h2>
        <button
          type="button"
          className="btn"
          disabled={busy}
          onClick={() => run('Direktif evaluate', () => postDirectivesEvaluate(userId))}
        >
          Kuralları çalıştır
        </button>
      </section>

      <section className="panel">
        <h2 className="panel-title">PDF rapor (Faza 6)</h2>
        <p className="muted small">Aktif direktifler ve özet sayılar.</p>
        {pdfAllowed === false && (
          <p className="muted small">
            PDF dışa aktarma Pro planda. Ayarlar → Abonelik planı üzerinden Pro’ya geçebilirsiniz.
          </p>
        )}
        <button
          type="button"
          className="btn"
          disabled={busy || pdfAllowed !== true}
          onClick={() => run('PDF indir', () => downloadAnalysisPdf().then(() => ({ ok: true })))}
        >
          Analiz PDF indir
        </button>
      </section>

      {log && <pre className="log-box">{log}</pre>}
    </div>
  )
}
