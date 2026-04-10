import { useEffect, useState } from 'react'
import { createCampaignMap, getCampaignMaps, getProducts } from '../api/client'
import type { CampaignMapItem, ProductResponse } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

export function CampaignMaps() {
  const { userId } = useUser()
  const [products, setProducts] = useState<ProductResponse[]>([])
  const [maps, setMaps] = useState<CampaignMapItem[]>([])
  const [campaignId, setCampaignId] = useState('')
  const [productId, setProductId] = useState(0)
  const [err, setErr] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)

  useEffect(() => {
    let c = false
    getProducts(userId)
      .then((p) => {
        if (c) return
        setProducts(p)
        if (p.length > 0) setProductId(p[0].id)
      })
      .catch(() => {})
    getCampaignMaps(userId)
      .then((m) => {
        if (!c) setMaps(m)
      })
      .catch(() => {})
    return () => {
      c = true
    }
  }, [userId])

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    setMsg(null)
    if (!campaignId.trim()) {
      setErr('Kampanya ID gerekli.')
      return
    }

    if (productId <= 0) {
      setErr('Önce en az bir ürün tanımlayın.')
      return
    }

    try {
      await createCampaignMap({ userId, campaignId: campaignId.trim(), productId })
      setMsg('Eşleme kaydedildi.')
      setCampaignId('')
      const [p, m] = await Promise.all([getProducts(userId), getCampaignMaps(userId)])
      setProducts(p)
      setMaps(m)
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Kayıt hatası')
    }
  }

  const productName = (id: number) => products.find((p) => p.id === id)?.name ?? `#${id}`

  return (
    <div className="page">
      <h1 className="page-title">Kampanya → ürün</h1>
      <p className="page-lead">
        Meta kampanya kimliğini (insights’taki <code>campaign_id</code>) ürünle eşleştirin; metrik
        hesapları bu haritayı kullanır.
      </p>

      {err && <p className="error-banner">{err}</p>}
      {msg && <p className="ok-banner">{msg}</p>}

      <section className="panel form-stack">
        <h2 className="panel-title">Yeni eşleme</h2>
        <form onSubmit={onSubmit}>
          <label>
            Kampanya ID (Meta)
            <input value={campaignId} onChange={(e) => setCampaignId(e.target.value)} required />
          </label>
          <label>
            Ürün
            <select
              value={productId}
              onChange={(e) => setProductId(Number(e.target.value))}
              disabled={products.length === 0}
            >
              {products.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
          <button type="submit" className="btn primary" disabled={products.length === 0}>
            Kaydet
          </button>
        </form>
      </section>

      <section className="panel">
        <h2 className="panel-title">Mevcut eşlemeler</h2>
        {maps.length === 0 ? (
          <p className="muted">Kayıt yok.</p>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Kampanya ID</th>
                  <th>Ürün</th>
                </tr>
              </thead>
              <tbody>
                {maps.map((m) => (
                  <tr key={m.id}>
                    <td className="mono">{m.campaignId}</td>
                    <td>{productName(m.productId)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
