import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getRawInsights } from '../api/client'
import type { RawInsightRow } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

export function Analysis() {
  const [searchParams] = useSearchParams()
  const { userId } = useUser()
  const [level, setLevel] = useState<string>(searchParams.get('level') ?? '')
  const [adsetId, setAdsetId] = useState<string>(searchParams.get('adsetId') ?? '')
  const [rows, setRows] = useState<RawInsightRow[]>([])
  const [err, setErr] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLevel(searchParams.get('level') ?? '')
    setAdsetId(searchParams.get('adsetId') ?? '')
  }, [searchParams])

  useEffect(() => {
    let c = false
    setLoading(true)
    setErr(null)
    getRawInsights(userId, level || undefined)
      .then((d) => {
        if (!c) setRows(d)
      })
      .catch((e: unknown) => {
        if (!c) setErr(e instanceof Error ? e.message : 'Veri alınamadı')
      })
      .finally(() => {
        if (!c) setLoading(false)
      })
    return () => {
      c = true
    }
  }, [userId, level])

  const visibleRows = adsetId ? rows.filter((r) => r.level === 'adset' && r.entityId === adsetId) : rows

  return (
    <div className="page">
      <h1 className="page-title">Ham metrikler</h1>
      <p className="page-lead">
        Senkron sonrası <code>raw_insights</code> kayıtları. ROAS/CPA hesaplanmış satırlarda gösterilir.
      </p>

      <div className="panel filter-bar">
        <label>
          Seviye filtresi
          <select value={level} onChange={(e) => setLevel(e.target.value)}>
            <option value="">Tümü</option>
            <option value="campaign">campaign</option>
            <option value="adset">adset</option>
            <option value="ad">ad</option>
          </select>
        </label>
        {adsetId && (
          <p className="muted small" style={{ margin: 0 }}>
            Adset filtresi aktif: <code>{adsetId}</code>
          </p>
        )}
      </div>

      {loading && <p className="muted">Yükleniyor…</p>}
      {err && <p className="error-banner">{err}</p>}

      {!loading && !err && visibleRows.length === 0 && (
        <p className="muted">Kayıt yok. Veri &amp; hesap sayfasından insights çekin.</p>
      )}

      {!loading && visibleRows.length > 0 && (
        <div className="table-wrap wide">
          <table className="data-table compact">
            <thead>
              <tr>
                <th>Seviye</th>
                <th>Varlık</th>
                <th>Tarih</th>
                <th>Harcama</th>
                <th>Göst.</th>
                <th>Satın</th>
                <th>ROAS</th>
                <th>CPA</th>
              </tr>
            </thead>
            <tbody>
              {visibleRows.map((r) => (
                <tr key={r.id}>
                  <td>{r.level}</td>
                  <td className="mono cell-clip" title={r.entityId}>
                    {r.entityName || r.entityId}
                  </td>
                  <td className="mono small">
                    {r.dateStart} → {r.dateStop}
                  </td>
                  <td>{r.spend.toFixed(2)}</td>
                  <td>{r.impressions}</td>
                  <td>{r.purchases}</td>
                  <td>{r.roas != null ? r.roas.toFixed(2) : '—'}</td>
                  <td>{r.cpa != null ? r.cpa.toFixed(2) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
